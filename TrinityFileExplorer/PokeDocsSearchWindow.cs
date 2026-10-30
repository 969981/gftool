using System.Text;
using System.Text.Json;

namespace TrinityFileExplorer;

/// <summary>Offline search/export dialog. Game payloads are never bundled with PokeDocs.</summary>
public sealed class PokeDocsSearchWindow : Form
{
    private readonly PokeDocsStore store;
    private readonly Func<string, IReadOnlyCollection<ulong>, string, bool> exportResources;
    private readonly Action<string> gameChanged;
    private readonly ComboBox games = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 95 };
    private readonly ComboBox scopes = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    private readonly TextBox query = new() { Width = 300, PlaceholderText = "Name, path, symbol or hexadecimal hash" };
    private readonly DataGridView grid = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = true,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        RowHeadersVisible = false
    };
    private readonly Label status = new() { AutoSize = true, Text = "Offline database ready." };
    private IReadOnlyList<PokeDocsStore.Hit> matches = Array.Empty<PokeDocsStore.Hit>();

    public PokeDocsSearchWindow(PokeDocsStore store, string game,
        Func<string, IReadOnlyCollection<ulong>, string, bool> exportResources,
        Action<string> gameChanged)
    {
        this.store = store;
        this.exportResources = exportResources;
        this.gameChanged = gameChanged;
        Text = "PokeDocs SQLite - Offline Search";
        Width = 1110;
        Height = 680;
        MinimumSize = new Size(750, 400);
        StartPosition = FormStartPosition.CenterParent;

        games.Items.AddRange(new object[] { "SV", "ZA", "LA", "LGPE", "SWSH" });
        games.SelectedItem = game;
        if (games.SelectedIndex < 0) games.SelectedIndex = 0;
        scopes.Items.AddRange(new object[] { "fd", "trpak", "all", "lua_c", "lua_e", "lua_f", "lua_s", "audio", "fbs" });
        scopes.SelectedIndex = 0;

        var searchButton = new Button { Text = "Search", AutoSize = true };
        searchButton.Click += async (_, _) => await SearchAsync();
        query.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await SearchAsync(); } };
        games.SelectedIndexChanged += async (_, _) =>
        {
            gameChanged((string)games.SelectedItem!);
            await SearchAsync();
        };
        scopes.SelectedIndexChanged += async (_, _) => await SearchAsync();

        var exportList = new Button { Text = "Export Results", AutoSize = true };
        exportList.Click += (_, _) => ExportList();
        var exportGame = new Button { Text = "Extract Selected Game Files", AutoSize = true };
        exportGame.Click += (_, _) => ExportGameFiles();
        var copy = new Button { Text = "Copy Hash / Path", AutoSize = true };
        copy.Click += (_, _) =>
        {
            var selected = SelectedHits();
            if (selected.Count > 0)
                Clipboard.SetText(string.Join(Environment.NewLine, selected.Select(x => $"{x.Hash} {x.Name}")));
        };

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = 84, AutoScroll = true,
            FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(8)
        };
        toolbar.Controls.Add(new Label { Text = "Game:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        toolbar.Controls.Add(games);
        toolbar.Controls.Add(new Label { Text = "Scope:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        toolbar.Controls.Add(scopes);
        toolbar.Controls.Add(query);
        toolbar.Controls.Add(searchButton);
        toolbar.Controls.Add(exportList);
        toolbar.Controls.Add(exportGame);
        toolbar.Controls.Add(copy);

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(10) };
        footer.Controls.Add(status);
        Controls.Add(grid);
        Controls.Add(footer);
        Controls.Add(toolbar);

        Shown += async (_, _) => await SearchAsync();
    }

    private async Task SearchAsync()
    {
        var game = (string)games.SelectedItem!;
        var scope = (string)scopes.SelectedItem!;
        var term = query.Text;
        status.Text = "Searching database...";
        try
        {
            var found = await Task.Run(() => store.Search(game, scope, term, 500));
            if (IsDisposed || game != (string)games.SelectedItem! || scope != (string)scopes.SelectedItem! || term != query.Text)
                return;
            matches = found;
            grid.DataSource = null;
            grid.DataSource = matches.ToList();
            status.Text = $"{found.Count} result(s) (maximum 500). Source: PokeDocs {store.Revision[..Math.Min(12, store.Revision.Length)]}. Offline search does not need RomFS.";
        }
        catch (Exception ex) { status.Text = $"Query failed: {ex.Message}"; }
    }

    private List<PokeDocsStore.Hit> SelectedHits() =>
        grid.SelectedRows.Cast<DataGridViewRow>()
            .Select(x => x.DataBoundItem).OfType<PokeDocsStore.Hit>().ToList();

    private void ExportList()
    {
        var selected = SelectedHits();
        var list = selected.Count > 0 ? selected : matches.ToList();
        if (list.Count == 0) { MessageBox.Show("No records to export."); return; }

        using var dialog = new SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv|JSON (*.json)|*.json|TXT (*.txt)|*.txt|FlatBuffers schema (*.fbs)|*.fbs",
            FileName = $"PokeDocs-{games.SelectedItem}-{scopes.SelectedItem}.csv"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        string ext = Path.GetExtension(dialog.FileName).ToLowerInvariant();
        if (ext == ".fbs")
        {
            if (list.Count != 1 || list[0].Scope != "fbs")
            {
                MessageBox.Show("Select exactly one FlatBuffers schema before exporting .fbs.");
                return;
            }
            File.WriteAllText(dialog.FileName, store.ReadSchema(list[0].Name) ?? string.Empty, new UTF8Encoding(false));
        }
        else if (ext == ".json")
        {
            File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        }
        else if (ext == ".txt")
        {
            File.WriteAllLines(dialog.FileName, list.Select(x => string.IsNullOrEmpty(x.Hash) ? x.Name : $"0x{x.Hash} {x.Name}"), new UTF8Encoding(false));
        }
        else
        {
            static string Csv(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
            var rows = new List<string> { "Game,Scope,Hash,Name,Source,Line" };
            rows.AddRange(list.Select(x => string.Join(",", new[] { x.Game, x.Scope, x.Hash, x.Name, x.Source, x.Line.ToString() }.Select(Csv))));
            File.WriteAllLines(dialog.FileName, rows, new UTF8Encoding(true));
        }
        status.Text = $"Exported {list.Count} index record(s) to {dialog.FileName}";
    }

    private void ExportGameFiles()
    {
        var selected = SelectedHits();
        var hashes = new List<ulong>();
        foreach (var hit in selected)
            if (hit.Scope is "fd" or "trpak" && ulong.TryParse(hit.Hash, System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out var hash))
                hashes.Add(hash);
        if (hashes.Count == 0)
        {
            MessageBox.Show("Select named FD/TRPAK hash records first. Index exports do not require RomFS; binary extraction does.");
            return;
        }
        using var folder = new FolderBrowserDialog { Description = "Output folder for selected game files" };
        if (folder.ShowDialog(this) != DialogResult.OK) return;
        if (exportResources((string)games.SelectedItem!, hashes.Distinct().ToArray(), folder.SelectedPath))
            status.Text = "Extraction request completed. Only files found in the selected RomFS can be extracted.";
    }
}
