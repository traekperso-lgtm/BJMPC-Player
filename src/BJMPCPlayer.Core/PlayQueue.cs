using System.ComponentModel;

namespace BJMPCPlayer.Core;

public enum RepeatMode { Off, All, One }

public sealed class QueueItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private string _title = "";
    private string? _artist;
    private double _duration;

    public QueueItem(string path, string? title = null, string? artist = null)
    {
        Path = path;
        Title = title ?? DefaultTitle(path);
        Artist = artist;
    }
    public string Path { get; }
    public string Title { get => _title; set { _title = value; Changed(nameof(Title)); } }
    public string? Artist { get => _artist; set { _artist = value; Changed(nameof(Artist)); } }
    public double Duration { get => _duration; set { _duration = value; Changed(nameof(Duration)); Changed(nameof(DurationText)); } }
    public string DurationText => _duration > 0 ? TimeFormat.Clock(_duration) : "";
    private void Changed(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    public bool IsVideo => MediaFormats.IsVideo(Path);

    public static string DefaultTitle(string path) =>
        MediaFormats.IsUrl(path) ? path : System.IO.Path.GetFileNameWithoutExtension(path);

    public override string ToString() => Title;
}

/// <summary>File d'attente : ordre, lecture aléatoire (sans répétition jusqu'à épuisement), modes de répétition.</summary>
public sealed class PlayQueue
{
    private readonly List<QueueItem> _items = new();
    private List<int> _order = new();     // ordre de lecture (indices dans _items)
    private int _pos = -1;                // position dans _order
    private bool _shuffle;
    private readonly Random _rng;

    public PlayQueue(int? seed = null) { _rng = seed is int s ? new Random(s) : new Random(); }

    public IReadOnlyList<QueueItem> Items => _items;
    public RepeatMode Repeat { get; set; } = RepeatMode.Off;
    public int CurrentIndex => _pos >= 0 && _pos < _order.Count ? _order[_pos] : -1;
    public QueueItem? Current => CurrentIndex >= 0 ? _items[CurrentIndex] : null;
    public int Count => _items.Count;

    public bool Shuffle
    {
        get => _shuffle;
        set { if (_shuffle != value) { _shuffle = value; Rebuild(CurrentIndex); } }
    }

    /// <summary>Remplace le contenu et se place sur <paramref name="start"/>.</summary>
    public QueueItem? Set(IEnumerable<QueueItem> items, int start = 0)
    {
        _items.Clear();
        _items.AddRange(items);
        if (_items.Count == 0) { _order.Clear(); _pos = -1; return null; }
        Rebuild(Math.Clamp(start, 0, _items.Count - 1));
        return Current;
    }

    public void Add(IEnumerable<QueueItem> items)
    {
        int before = _items.Count;
        _items.AddRange(items);
        var added = Enumerable.Range(before, _items.Count - before).ToList();
        if (_shuffle) Shuffled(added);
        _order.AddRange(added);
        if (_pos < 0 && _items.Count > 0) _pos = 0;
    }

    /// <summary>Insère juste après le morceau en cours (« Lire ensuite »).</summary>
    public void PlayNext(IEnumerable<QueueItem> items)
    {
        var list = items.ToList();
        if (_items.Count == 0) { Set(list); return; }
        int insertAt = CurrentIndex + 1;
        _items.InsertRange(insertAt, list);
        for (int i = 0; i < _order.Count; i++) if (_order[i] >= insertAt) _order[i] += list.Count;
        _order.InsertRange(_pos + 1, Enumerable.Range(insertAt, list.Count));
    }

    public void RemoveAt(int index)
    {
        if (index < 0 || index >= _items.Count) return;
        int cur = CurrentIndex;
        _items.RemoveAt(index);
        int p = _order.IndexOf(index);
        _order.RemoveAt(p);
        for (int i = 0; i < _order.Count; i++) if (_order[i] > index) _order[i]--;
        if (p < _pos || (p == _pos && _pos >= _order.Count)) _pos--;
        if (_items.Count == 0) _pos = -1;
        else if (cur == index) _pos = Math.Clamp(_pos, 0, _order.Count - 1);
    }

    /// <summary>Nouvel ordre après un glisser-déposer dans la file (mêmes éléments, ordre différent).</summary>
    public void Reorder(IList<QueueItem> newOrder)
    {
        var cur = Current;
        _items.Clear();
        _items.AddRange(newOrder);
        Rebuild(cur != null ? _items.IndexOf(cur) : 0);
    }

    public void Clear() { _items.Clear(); _order.Clear(); _pos = -1; }

    public QueueItem? MoveTo(int index)
    {
        if (index < 0 || index >= _items.Count) return null;
        if (_shuffle) Rebuild(index); else _pos = _order.IndexOf(index);
        return Current;
    }

    /// <summary>Morceau suivant. <paramref name="auto"/> = fin naturelle (respecte « répéter un »).</summary>
    public QueueItem? Next(bool auto = false)
    {
        if (_items.Count == 0) return null;
        if (auto && Repeat == RepeatMode.One) return Current;
        if (_pos + 1 < _order.Count) { _pos++; return Current; }
        if (Repeat == RepeatMode.Off) return auto ? null : Current;
        if (_shuffle) { Rebuild(-1); _pos = 0; } else _pos = 0;
        return Current;
    }

    public QueueItem? Previous()
    {
        if (_items.Count == 0) return null;
        if (_pos > 0) _pos--;
        else if (Repeat == RepeatMode.All) _pos = _order.Count - 1;
        return Current;
    }

    private void Rebuild(int first)
    {
        var all = Enumerable.Range(0, _items.Count).ToList();
        if (_shuffle)
        {
            if (first >= 0) all.Remove(first);
            Shuffled(all);
            if (first >= 0) all.Insert(0, first);
            _order = all;
            _pos = _order.Count > 0 ? 0 : -1;
        }
        else
        {
            _order = all;
            _pos = first >= 0 ? first : (_order.Count > 0 ? 0 : -1);
        }
    }

    private void Shuffled(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
