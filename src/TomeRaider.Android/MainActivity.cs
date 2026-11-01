using Android.App;
using Android.Content.PM;
using Avalonia.Android;

namespace TomeRaider.Android;

[Activity(
    Label = "Tome Raider",
    Theme = "@style/TomeRaiderTheme.NoActionBar",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation
        | ConfigChanges.ScreenSize
        | ConfigChanges.UiMode)]
public sealed class MainActivity : AvaloniaMainActivity
{
}
