namespace Measures.Controls;

/// <summary>
/// An element the glyph field follows. The field re-reads the rectangle every
/// frame anyway; these two say when it is worth watching closely - while a hand
/// is moving it, and at the moment it lands.
/// </summary>
public interface IFieldSource
{
    /// <summary>Raised while the element is being moved by hand.</summary>
    event EventHandler? Moved;

    /// <summary>Raised when it is let go.</summary>
    event EventHandler? Released;
}
