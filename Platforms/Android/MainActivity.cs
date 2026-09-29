using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.View;

namespace asdfadfasddf;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);

		if (Window is { } window)
		{
			WindowCompat.SetDecorFitsSystemWindows(window, false);
			if (!OperatingSystem.IsAndroidVersionAtLeast(35))
				window.SetStatusBarColor(Android.Graphics.Color.Transparent);
			new WindowInsetsControllerCompat(window, window.DecorView).AppearanceLightStatusBars = false;
		}
	}
}
