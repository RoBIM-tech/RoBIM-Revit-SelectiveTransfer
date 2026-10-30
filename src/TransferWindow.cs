using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using RoBIM.Wpf;

namespace RoBIM.Feature;

public sealed class TransferWindow : Window
{
    private readonly TransferEngine engine;
    private readonly TextBox sourcePathBox = new() { IsReadOnly = true };
    private readonly Button browseBtn = new() { Content = "찾아보기...", MinWidth = 90, Margin = new Thickness(6, 0, 0, 0) };
    private readonly Button scanBtn = new() { Content = "파일 분석", MinWidth = 90, Margin = new Thickness(6, 0, 0, 0), IsEnabled = false };

    private readonly ComboBox categoryCombo = new() { DisplayMemberPath = "CategoryTitle", MinWidth = 200 };
    private readonly Button selectAllBtn = new() { Content = "전체 선택", Margin = new Thickness(0, 0, 6, 0) };
    private readonly Button unselectAllBtn = new() { Content = "전체 해제", Margin = new Thickness(0, 0, 6, 0) };
    private readonly Button selectNewOnlyBtn = new() { Content = "신규 항목만 선택", Margin = new Thickness(0, 0, 6, 0) };

    private readonly ComboBox conflictCombo = new() { MinWidth = 180 };
    private readonly DataGrid grid = new()
    {
        AutoGenerateColumns = false,
        CanUserAddRows = false,
        CanUserDeleteRows = false,
        SelectionMode = DataGridSelectionMode.Single,
        HeadersVisibility = DataGridHeadersVisibility.All
    };

    private readonly ProgressBar progressBar = new() { Height = 6, Margin = new Thickness(0, 6, 0, 6), Visibility = Visibility.Collapsed, IsIndeterminate = true };
    private readonly TextBlock statusText = new() { Text = "원본 RVT 파일을 선택한 후 [파일 분석]을 클릭하세요.", TextWrapping = TextWrapping.Wrap, MaxWidth = 520 };
    private readonly Button executeBtn = new() { Content = "선택 항목 가져오기", MinWidth = 130, Margin = new Thickness(6, 0, 0, 0), IsEnabled = false };
    private readonly Button closeBtn = new() { Content = "닫기", MinWidth = 80, Margin = new Thickness(6, 0, 0, 0) };

    private readonly Dictionary<string, ObservableCollection<TransferItem>> itemsByCategory = [];
    private string currentCategoryKey = string.Empty;

    public TransferWindow(TransferEngine engine, string version)
    {
        this.engine = engine;
        RoBimTheme.Apply(this);
        Title = $"선택적 표준 전송 · {version} · TEST";
        Width = 880;
        Height = 650;
        MinWidth = 720;
        MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        InitializeUI();
        BindEvents();
    }

    private void InitializeUI()
    {
        var root = new DockPanel { Margin = RoBimTheme.ContentMargin };
        Content = root;

        // Top Header
        var topPanel = new StackPanel();
        DockPanel.SetDock(topPanel, Dock.Top);
        root.Children.Add(topPanel);

        topPanel.Children.Add(RoBimTheme.Heading("선택적 표준 전송 (Selective Standard Transfer)"));
        topPanel.Children.Add(new TextBlock
        {
            Text = "다른 RVT 파일에서 필요한 작업 세트(Workset), 뷰 템플릿(View Template)을 선별하여 현재 프로젝트에 가져옵니다.",
            Margin = new Thickness(0, 0, 0, 12)
        });

        // File selection bar
        var fileRow = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        DockPanel.SetDock(browseBtn, Dock.Right);
        DockPanel.SetDock(scanBtn, Dock.Right);
        fileRow.Children.Add(browseBtn);
        fileRow.Children.Add(scanBtn);
        fileRow.Children.Add(sourcePathBox);
        topPanel.Children.Add(fileRow);

        // Category & Action row
        var filterRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var leftFilter = new StackPanel { Orientation = Orientation.Horizontal };
        leftFilter.Children.Add(new TextBlock { Text = "항목 구분: ", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        leftFilter.Children.Add(categoryCombo);
        filterRow.Children.Add(leftFilter);

        var rightFilter = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        rightFilter.Children.Add(selectNewOnlyBtn);
        rightFilter.Children.Add(selectAllBtn);
        rightFilter.Children.Add(unselectAllBtn);
        filterRow.Children.Add(rightFilter);
        topPanel.Children.Add(filterRow);

        topPanel.Children.Add(progressBar);

        // DataGrid Setup
        grid.Columns.Add(new DataGridCheckBoxColumn
        {
            Header = "선택",
            Binding = new Binding(nameof(TransferItem.IsSelected)) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
            Width = 60
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "항목 이름",
            Binding = new Binding(nameof(TransferItem.Name)),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star),
            IsReadOnly = true
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "분류",
            Binding = new Binding(nameof(TransferItem.Category)),
            Width = 180,
            IsReadOnly = true
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "상태",
            Binding = new Binding(nameof(TransferItem.StatusDescription)),
            Width = 110,
            IsReadOnly = true
        });

        root.Children.Add(grid);

        // Bottom Footer
        conflictCombo.ItemsSource = new[] { "중복 항목 건너뛰기 (기존 유지)", "중복 시 새 이름으로 복사" };
        conflictCombo.SelectedIndex = 0;

        var conflictPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        conflictPanel.Children.Add(new TextBlock { Text = "중복 처리: ", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        conflictPanel.Children.Add(conflictCombo);

        var summaryContainer = new StackPanel();
        summaryContainer.Children.Add(conflictPanel);
        summaryContainer.Children.Add(statusText);

        RoBimTheme.Primary(executeBtn);
        var footer = RoBimTheme.Footer(summaryContainer, closeBtn, executeBtn);
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        // Populate Category Combo
        categoryCombo.ItemsSource = engine.Handlers;
        if (engine.Handlers.Count > 0)
        {
            categoryCombo.SelectedIndex = 0;
            currentCategoryKey = engine.Handlers[0].CategoryKey;
        }
    }

    private void BindEvents()
    {
        browseBtn.Click += (_, _) => BrowseSourceFile();
        scanBtn.Click += (_, _) => ScanFile();
        categoryCombo.SelectionChanged += (_, _) => SwitchCategory();

        selectAllBtn.Click += (_, _) => SetSelection(true);
        unselectAllBtn.Click += (_, _) => SetSelection(false);
        selectNewOnlyBtn.Click += (_, _) => SelectNewOnly();

        executeBtn.Click += (_, _) => ExecuteTransfer();
        closeBtn.Click += (_, _) => Close();
    }

    private void BrowseSourceFile()
    {
        var ofd = new OpenFileDialog
        {
            Filter = "Revit Project (*.rvt)|*.rvt",
            Title = "가져올 원본 Revit 프로젝트 선택"
        };
        if (ofd.ShowDialog(this) == true)
        {
            sourcePathBox.Text = ofd.FileName;
            scanBtn.IsEnabled = true;
            statusText.Text = $"선택된 파일: {Path.GetFileName(ofd.FileName)}. [파일 분석] 버튼을 눌러 항목을 검색하세요.";
        }
    }

    private void ScanFile()
    {
        string path = sourcePathBox.Text;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            MessageBox.Show(this, "유효한 RVT 파일 경로를 선택하세요.", "안내", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        progressBar.Visibility = Visibility.Visible;
        statusText.Text = "원본 RVT 파일을 분석하는 중입니다...";
        scanBtn.IsEnabled = false;
        browseBtn.IsEnabled = false;

        try
        {
            var scanned = engine.ScanSource(path);
            itemsByCategory.Clear();
            foreach (var kvp in scanned)
            {
                itemsByCategory[kvp.Key] = new ObservableCollection<TransferItem>(kvp.Value);
            }

            SwitchCategory();
            int total = itemsByCategory.Values.Sum(c => c.Count);
            int newCount = itemsByCategory.Values.Sum(c => c.Count(i => !i.ExistsInTarget));
            statusText.Text = $"분석 완료: 총 {total}개 항목 감지 (신규: {newCount}개). 가져올 항목을 선택하세요.";
            executeBtn.IsEnabled = total > 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"파일 분석 중 오류가 발생했습니다:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            statusText.Text = "분석 실패: " + ex.Message;
        }
        finally
        {
            progressBar.Visibility = Visibility.Collapsed;
            scanBtn.IsEnabled = true;
            browseBtn.IsEnabled = true;
        }
    }

    private void SwitchCategory()
    {
        if (categoryCombo.SelectedItem is ITransferHandler handler)
        {
            currentCategoryKey = handler.CategoryKey;
            if (itemsByCategory.TryGetValue(currentCategoryKey, out var items))
            {
                grid.ItemsSource = items;
            }
            else
            {
                grid.ItemsSource = null;
            }
        }
    }

    private void SetSelection(bool select)
    {
        if (grid.ItemsSource is ObservableCollection<TransferItem> list)
        {
            foreach (var item in list) item.IsSelected = select;
        }
    }

    private void SelectNewOnly()
    {
        if (grid.ItemsSource is ObservableCollection<TransferItem> list)
        {
            foreach (var item in list) item.IsSelected = !item.ExistsInTarget;
        }
    }

    private void ExecuteTransfer()
    {
        string path = sourcePathBox.Text;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

        var selectedDict = new Dictionary<string, IReadOnlyList<TransferItem>>();
        foreach (var kvp in itemsByCategory)
        {
            selectedDict[kvp.Key] = kvp.Value.ToList();
        }

        int totalSelected = selectedDict.Values.Sum(c => c.Count(i => i.IsSelected));
        if (totalSelected == 0)
        {
            MessageBox.Show(this, "가져올 항목이 하나도 선택되지 않았습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var conflict = conflictCombo.SelectedIndex == 1 ? ConflictResolution.Rename : ConflictResolution.Skip;

        progressBar.Visibility = Visibility.Visible;
        executeBtn.IsEnabled = false;

        try
        {
            var res = engine.Execute(path, selectedDict, conflict, msg => statusText.Text = msg);

            string summaryMsg = $"[전송 완료]\n- 생성/복사: {res.CreatedCount}개\n- 건너뜀: {res.SkippedCount}개\n- 오류: {res.ErrorCount}개";
            if (res.Messages.Count > 0)
            {
                summaryMsg += "\n\n세부 내용:\n" + string.Join("\n", res.Messages.Take(5));
            }

            MessageBox.Show(this, summaryMsg, "처리 결과", MessageBoxButton.OK,
                res.ErrorCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

            // 완료 후 항목 재분석
            ScanFile();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"전송 중 오류 발생:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            progressBar.Visibility = Visibility.Collapsed;
            executeBtn.IsEnabled = true;
        }
    }
}
