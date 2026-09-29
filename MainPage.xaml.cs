using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace asdfadfasddf;

public sealed record BookEntry(string Title, string Shelf, string Section, int Floor)
{
	public string FloorLabel => $"FLOOR {Floor}";
}

public partial class MainPage : ContentPage
{
	private readonly List<BookEntry> books = [];
	private readonly ObservableCollection<BookEntry> results = [];
	private int currentFloor = 1;
	private double mapWidth;
	private double mapZoom = 1;
	private const string WikiBase = "https://librarian-tidy-up-the-arcane-library.fandom.com/wiki/";

	public MainPage()
	{
		InitializeComponent();
		ResultsList.ItemsSource = results;
		ShowFloor(1);
		SizeChanged += (_, _) => UpdateMapSize();
		_ = LoadBooksAsync();
	}

	private async Task LoadBooksAsync()
	{
		try
		{
			for (var floor = 1; floor <= 2; floor++)
			{
				var fileName = floor == 1 ? "first_floor.json" : "second_floor.json";
				using var stream = await FileSystem.OpenAppPackageFileAsync(fileName);
				using var document = await JsonDocument.ParseAsync(stream);
				var wikiText = document.RootElement.GetProperty("parse").GetProperty("wikitext")
					.GetProperty("*").GetString() ?? "";
				books.AddRange(ParseBooks(wikiText, floor));
			}

			SearchBooks();
		}
		catch (Exception)
		{
			await DisplayAlertAsync("Catalog unavailable", "The book catalog could not be loaded.", "OK");
		}
	}

	private static IEnumerable<BookEntry> ParseBooks(string wikiText, int floor)
	{
		var shelf = "";
		var section = "";
		foreach (var line in wikiText.Split('\n'))
		{
			var heading = Regex.Match(line, @"^==\s*(?<shelf>[12][A-Q])\s*:\s*(?<section>.*?)\s*==");
			if (heading.Success)
			{
				shelf = heading.Groups["shelf"].Value;
				section = heading.Groups["section"].Value.Trim();
				continue;
			}

			var title = Regex.Match(line, @"^\*+\s+(?<title>.+?)\s*$", RegexOptions.Multiline);
			if (shelf.Length > 0 && title.Success && !Regex.IsMatch(title.Groups["title"].Value, @"^\d+-book series$"))
				yield return new BookEntry(title.Groups["title"].Value.Trim(), shelf, section, floor);
		}
	}

	private void OnSearchTextChanged(object? sender, TextChangedEventArgs e) => SearchBooks();

	private void SearchBooks()
	{
		var query = BookSearch.Text?.Trim() ?? "";
		results.Clear();
		if (query.Length == 0)
		{
			ResultsList.IsVisible = false;
			MapPanel.IsVisible = true;
			SelectedShelfPanel.IsVisible = false;
			return;
		}

		var compare = CultureInfo.InvariantCulture.CompareInfo;
		var options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
		foreach (var book in books.Where(book => query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
					 .All(word => compare.IndexOf($"{book.Title} {book.Section} {book.Shelf}", word, options) >= 0))
					 .OrderByDescending(book => compare.IsPrefix(book.Title, query, options))
					 .ThenBy(book => book.Title).Take(100))
			results.Add(book);

		ResultsList.IsVisible = true;
		MapPanel.IsVisible = false;
	}

	private void OnResultSelected(object? sender, SelectionChangedEventArgs e)
	{
		if (e.CurrentSelection.FirstOrDefault() is not BookEntry book)
			return;

		ResultsList.SelectedItem = null;
		ShowFloor(book.Floor);
		SelectedShelfLabel.Text = $"{book.Shelf} / {book.Section}";
		SelectedBookLabel.Text = book.Title;
		SelectedShelfPanel.IsVisible = true;
		ResultsList.IsVisible = false;
		MapPanel.IsVisible = true;
	}

	private void ShowFloor(int floor)
	{
		currentFloor = floor;
		FloorHeading.Text = floor == 1 ? "First floor" : "Second floor";
		FloorMap.Source = floor == 1 ? "first_floor.jpg" : "second_floor.jpg";
		UpdateMapSize();
		FirstFloorButton.BackgroundColor = Color.FromArgb(floor == 1 ? "#18362F" : "#DAE6DF");
		SecondFloorButton.BackgroundColor = Color.FromArgb(floor == 2 ? "#18362F" : "#DAE6DF");
		FirstFloorButton.TextColor = Color.FromArgb(floor == 1 ? "#FFFFFF" : "#18362F");
		SecondFloorButton.TextColor = Color.FromArgb(floor == 2 ? "#FFFFFF" : "#18362F");
	}

	private void OnFirstFloorClicked(object? sender, EventArgs e) => SelectFloor(1);
	private void OnSecondFloorClicked(object? sender, EventArgs e) => SelectFloor(2);

	private void SelectFloor(int floor)
	{
		ShowFloor(floor);
		SelectedShelfPanel.IsVisible = false;
		ResultsList.IsVisible = false;
		MapPanel.IsVisible = true;
	}

	private void OnBackToResultsClicked(object? sender, EventArgs e) => SearchBooks();

	private void OnZoomOutClicked(object? sender, EventArgs e)
	{
		mapZoom = Math.Max(1, mapZoom - 0.5);
		UpdateMapSize();
	}

	private void OnZoomInClicked(object? sender, EventArgs e)
	{
		mapZoom = Math.Min(4, mapZoom + 0.5);
		UpdateMapSize();
	}

	private void UpdateMapSize()
	{
		var availableWidth = Math.Max(280, Width - 34);
		var mapHeight = availableWidth * mapZoom * (currentFloor == 1 ? 761d / 1290 : 771d / 1283);
		if (Math.Abs(mapWidth - availableWidth) < 1 && FloorMap.WidthRequest == availableWidth * mapZoom && FloorMap.HeightRequest == mapHeight)
			return;

		mapWidth = availableWidth;
		FloorMap.WidthRequest = mapWidth * mapZoom;
		FloorMap.HeightRequest = mapHeight;
	}

	private void OnFloorGuideTapped(object? sender, TappedEventArgs e) =>
		_ = Launcher.Default.OpenAsync(new Uri(WikiBase + (currentFloor == 1 ? "First_Floor" : "Second_Floor")));

	private void OnMapCreditTapped(object? sender, TappedEventArgs e) =>
		_ = Launcher.Default.OpenAsync(new Uri(WikiBase + (currentFloor == 1 ? "File:First_Floor.jpg" : "File:Second_Floor.jpg")));

	private void OnLicenseTapped(object? sender, TappedEventArgs e) =>
		_ = Launcher.Default.OpenAsync(new Uri("https://www.fandom.com/licensing"));
}
