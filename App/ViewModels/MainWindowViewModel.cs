using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Core;
using Import;

namespace App.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private List<Part> _allParts = [];
    
    private string _gameDataPath = "";
    public string GameDataPath
    {
        get => _gameDataPath;
        set { _gameDataPath = value; OnPropertyChanged(); }
    }
    
    private ObservableCollection<Part> _parts = [];

    public ObservableCollection<Part> Parts
    {
        get => _parts;
        set
        {
            _parts = value;
            OnPropertyChanged();
        }
    }
    
    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            OnPropertyChanged();
            ApplyFilter();
        }
    }

    private string _selectedCategory = "All";
    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            _selectedCategory = value;
            OnPropertyChanged();
            ApplyFilter();
        }
    }
    
    private ObservableCollection<string> _availableCategories = [];

    public ObservableCollection<string> AvailableCategories
    {
        get => _availableCategories;
        set
        {
            _availableCategories = value; 
            OnPropertyChanged();
        }
    }

    private Part? _selectedPart;
    public Part? SelectedPart
    {
        get => _selectedPart;
        set
        {
            _selectedPart = value; 
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedPart.ImportantSpecs));
            OnPropertyChanged(nameof(SelectedPart.HasImportantSpecs));
        }
    }

    private string _statusText = "Ready to scan.";
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    private async void ApplyFilter()
    {
        try
        {
            var search = SearchText ?? string.Empty;
            var category = SelectedCategory ?? "All";

            StatusText = "Filtering data matrix...";

            var filteredList = await Task.Run(() =>
            {
                var query = _allParts.AsEnumerable();

                if (!string.Equals(category, "All", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(p => string.Equals(p.Category, category, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(p => 
                        (p.Title != null && p.Title.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                        (p.Name != null && p.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                    );
                }

                return query.ToList();
            });

            Parts = new ObservableCollection<Part>(filteredList);
            StatusText = $"Showing {filteredList.Count} of {_allParts.Count} parts.";
        }
        catch (Exception ex)
        {
            StatusText = $"FILTER_ERROR // {ex.Message.ToUpper()}";
            Console.WriteLine($"Filtering exception: {ex}");
        }
    }

    public async Task ScanDirectoryAsync(string directory)
    {
        StatusText = "Scanning GameData directory...";
    
        var parsedResults = await Task.Run(() => PartScanner.ScanGameData(directory));
        _allParts = (parsedResults ?? [])
            .Where(p => p != null && (!string.IsNullOrWhiteSpace(p.Name) || !string.IsNullOrWhiteSpace(p.Title)))
            .ToList();

        var uniqueCategories = await Task.Run(() => 
        {
            return _allParts
                .Select(p => p.Category)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c)
                .ToList();
        });

        AvailableCategories = new ObservableCollection<string>(["All", .. uniqueCategories]);

        _selectedCategory = "All";
        OnPropertyChanged(nameof(SelectedCategory));

        Parts = new ObservableCollection<Part>(_allParts);
        StatusText = $"Scan Complete: Found {_allParts.Count} Parts";
    }
    
    public void ChangeCategory(string targetCategory)
    {
        SelectedCategory = targetCategory;
    }
}