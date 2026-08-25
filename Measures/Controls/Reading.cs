namespace Measures.Controls;

/// <summary>
/// What a chart has to say. Immediate readings are hover previews - the pointer
/// is already moving, so the caption swaps without the dip.
/// </summary>
public readonly record struct Reading(string Text, bool Live, bool Immediate = false)
{
    public static Reading Preview(string text) => new(text, Live: true, Immediate: true);

    public static Reading Committed(string text) => new(text, Live: true);

    public static Reading Default(string text) => new(text, Live: false);
}

/// <summary>
/// Implemented by every chart. The card owns the caption; the chart only says
/// what it reads, which keeps chart controls free of any chrome.
/// </summary>
public interface ICaptionSource
{
    event EventHandler<Reading>? ReadingChanged;

    /// <summary>Speaks the opening line once the chart is laid out.</summary>
    void PublishInitialReading();
}
