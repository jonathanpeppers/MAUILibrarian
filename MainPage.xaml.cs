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
	private double expandedZoom = 1;
#if ANDROID
	private Android.Content.PM.ScreenOrientation previousOrientation;
	private MainActivity? touchActivity;
	private Android.Views.ScaleGestureDetector? scaleDetector;
	private bool mapTouchActive;
#endif
	private const string WikiBase = "https://librarian-tidy-up-the-arcane-library.fandom.com/wiki/";

	public MainPage()
	{
		InitializeComponent();
		ResultsList.ItemsSource = results;
		ShowFloor(1);
		SizeChanged += (_, _) => UpdateMapSize();
		ExpandedScroller.SizeChanged += (_, _) => UpdateExpandedMapSize();
#if ANDROID
		Loaded += OnPageLoaded;
		Unloaded += OnPageUnloaded;
#endif
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
		ExpandedFloorMap.Source = FloorMap.Source;
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

	private void ZoomMap(bool expanded, double factor, double focusX, double focusY)
	{
		var scroller = expanded ? ExpandedScroller : MapScroller;
		var image = expanded ? ExpandedFloorMap : FloorMap;
		var oldZoom = expanded ? expandedZoom : mapZoom;
		var nextZoom = Math.Clamp(oldZoom * factor, 1, 4);
		if (nextZoom == oldZoom || image.WidthRequest <= 0 || image.HeightRequest <= 0)
			return;

		var originX = (focusX + scroller.ScrollX - image.X) / image.WidthRequest;
		var originY = (focusY + scroller.ScrollY - image.Y) / image.HeightRequest;
		if (expanded)
		{
			expandedZoom = nextZoom;
			UpdateExpandedMapSize();
		}
		else
		{
			mapZoom = nextZoom;
			UpdateMapSize();
		}

		var imageX = Math.Max(0, (scroller.Width - image.WidthRequest) / 2);
		var imageY = Math.Max(0, (scroller.Height - image.HeightRequest) / 2);
		var scrollX = Math.Clamp(imageX + originX * image.WidthRequest - focusX,
			0, Math.Max(0, image.WidthRequest - scroller.Width));
		var scrollY = Math.Clamp(imageY + originY * image.HeightRequest - focusY,
			0, Math.Max(0, image.HeightRequest - scroller.Height));
		scroller.Dispatcher.Dispatch(() => _ = scroller.ScrollToAsync(scrollX, scrollY, false));
	}

#if ANDROID
	private void OnPageLoaded(object? sender, EventArgs e)
	{
		if (Platform.CurrentActivity is not MainActivity activity)
			return;

		touchActivity = activity;
		scaleDetector = new Android.Views.ScaleGestureDetector(activity, new MapScaleListener(this));
		activity.TouchDispatched += OnTouchDispatched;
	}

	private void OnPageUnloaded(object? sender, EventArgs e)
	{
		if (touchActivity is not null)
			touchActivity.TouchDispatched -= OnTouchDispatched;
		touchActivity = null;
		scaleDetector?.Dispose();
		scaleDetector = null;
	}

	private void OnTouchDispatched(Android.Views.MotionEvent motionEvent)
	{
		if (motionEvent.ActionMasked == Android.Views.MotionEventActions.Down)
		{
			var scroller = ExpandedMap.IsVisible ? ExpandedScroller : MapScroller;
			var nativeView = scroller.Handler?.PlatformView as Android.Views.View;
			var location = new int[2];
			nativeView?.GetLocationInWindow(location);
			mapTouchActive = nativeView is not null && (ExpandedMap.IsVisible || MapPanel.IsVisible)
				&& motionEvent.GetX() >= location[0] && motionEvent.GetX() < location[0] + nativeView.Width
				&& motionEvent.GetY() >= location[1] && motionEvent.GetY() < location[1] + nativeView.Height;
		}

		if (mapTouchActive)
			scaleDetector?.OnTouchEvent(motionEvent);

		if (motionEvent.ActionMasked is Android.Views.MotionEventActions.Up or Android.Views.MotionEventActions.Cancel)
			mapTouchActive = false;
	}

	private sealed class MapScaleListener(MainPage page) : Android.Views.ScaleGestureDetector.SimpleOnScaleGestureListener
	{
		public override bool OnScaleBegin(Android.Views.ScaleGestureDetector? detector) => true;

		public override bool OnScale(Android.Views.ScaleGestureDetector? detector)
		{
			if (detector is null)
				return false;

			var expanded = page.ExpandedMap.IsVisible;
			var scroller = expanded ? page.ExpandedScroller : page.MapScroller;
			if (scroller.Handler?.PlatformView is not Android.Views.View nativeView)
				return false;

			var location = new int[2];
			nativeView.GetLocationInWindow(location);
			var density = nativeView.Resources?.DisplayMetrics?.Density ?? 1;
			page.ZoomMap(expanded, detector.ScaleFactor,
				(detector.FocusX - location[0]) / density, (detector.FocusY - location[1]) / density);
			return true;
		}
	}
#endif

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

	private void OnMapTapped(object? sender, TappedEventArgs e)
	{
		expandedZoom = 1;
		ExpandedMap.IsVisible = true;
		_ = ExpandedScroller.ScrollToAsync(0, 0, false);
#if ANDROID
		if (Platform.CurrentActivity is { } activity && activity.Window is { } window)
		{
			previousOrientation = activity.RequestedOrientation;
			activity.RequestedOrientation = Android.Content.PM.ScreenOrientation.Landscape;
			new AndroidX.Core.View.WindowInsetsControllerCompat(window, window.DecorView)
				.Hide(AndroidX.Core.View.WindowInsetsCompat.Type.SystemBars());
		}
#endif
		UpdateExpandedMapSize();
	}

	private void OnCloseExpandedMapClicked(object? sender, EventArgs e) => CloseExpandedMap();

	private void CloseExpandedMap()
	{
		ExpandedMap.IsVisible = false;
#if ANDROID
		if (Platform.CurrentActivity is { } activity && activity.Window is { } window)
		{
			new AndroidX.Core.View.WindowInsetsControllerCompat(window, window.DecorView)
				.Show(AndroidX.Core.View.WindowInsetsCompat.Type.SystemBars());
			activity.RequestedOrientation = previousOrientation;
		}
#endif
	}

	protected override bool OnBackButtonPressed()
	{
		if (!ExpandedMap.IsVisible)
			return base.OnBackButtonPressed();

		CloseExpandedMap();
		return true;
	}

	private void UpdateExpandedMapSize()
	{
		if (ExpandedScroller.Width <= 0 || ExpandedScroller.Height <= 0)
			return;

		var imageWidth = currentFloor == 1 ? 1290d : 1283d;
		var imageHeight = currentFloor == 1 ? 761d : 771d;
		var scale = Math.Min(ExpandedScroller.Width / imageWidth, ExpandedScroller.Height / imageHeight) * expandedZoom;
		ExpandedFloorMap.WidthRequest = imageWidth * scale;
		ExpandedFloorMap.HeightRequest = imageHeight * scale;
	}

	private void OnFloorGuideTapped(object? sender, TappedEventArgs e) =>
		_ = Launcher.Default.OpenAsync(new Uri(WikiBase + (currentFloor == 1 ? "First_Floor" : "Second_Floor")));

	private void OnMapCreditTapped(object? sender, TappedEventArgs e) =>
		_ = Launcher.Default.OpenAsync(new Uri(WikiBase + (currentFloor == 1 ? "File:First_Floor.jpg" : "File:Second_Floor.jpg")));

	private void OnLicenseTapped(object? sender, TappedEventArgs e) =>
		_ = Launcher.Default.OpenAsync(new Uri("https://www.fandom.com/licensing"));
}
