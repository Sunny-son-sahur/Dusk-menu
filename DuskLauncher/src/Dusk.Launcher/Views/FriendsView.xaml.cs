using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Dusk.Launcher.Views;

public partial class FriendsView : UserControl
{
    private sealed class FriendEntry
    {
        public string Id   { get; set; } = "";
        public string Note { get; set; } = "";
    }

    private static readonly string FriendsPath = Path.Combine(App.DataDir, "friends.json");
    private List<FriendEntry> _friends = new();

    public FriendsView()
    {
        InitializeComponent();
        Refresh();
    }

    public void Refresh()
    {
        Load();
        Render();
    }

    private void Load()
    {
        try
        {
            if (File.Exists(FriendsPath))
                _friends = JsonSerializer.Deserialize<List<FriendEntry>>(File.ReadAllText(FriendsPath)) ?? new();
        }
        catch
        {
            _friends = new();
        }
    }

    private void Save() =>
        File.WriteAllText(FriendsPath, JsonSerializer.Serialize(_friends, new JsonSerializerOptions { WriteIndented = true }));

    private void Render()
    {
        FriendList.ItemsSource = null;
        FriendList.Items.Clear();

        bool any = _friends.Count > 0;
        EmptyText.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
        FriendList.Visibility = any ? Visibility.Visible : Visibility.Collapsed;

        foreach (var f in _friends)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
            panel.Children.Add(new TextBlock
            {
                Text = f.Id,
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Consolas"),
                VerticalAlignment = VerticalAlignment.Center
            });
            if (!string.IsNullOrWhiteSpace(f.Note))
                panel.Children.Add(new TextBlock
                {
                    Text = " — " + f.Note,
                    Foreground = FindResource("TextDimBrush") as Brush ?? Brushes.Gray,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0)
                });
            FriendList.Items.Add(panel);
        }
    }

    private void OnAdd(object sender, RoutedEventArgs e)
    {
        string id = DiscordIdBox.Text.Trim();
        string note = NoteBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(id) || id == "Discord user ID")
            return;

        _friends.Add(new FriendEntry { Id = id, Note = note });
        Save();
        Render();
        DiscordIdBox.Text = "Discord user ID";
        NoteBox.Text = "Note (optional)";
    }

    private void OnRemove(object sender, RoutedEventArgs e)
    {
        if (FriendList.SelectedIndex < 0 || FriendList.SelectedIndex >= _friends.Count)
            return;
        _friends.RemoveAt(FriendList.SelectedIndex);
        Save();
        Render();
    }
}