using TedToolkit.Annotations.Boxing;

namespace TedToolkit.Annotations.Boxing.Tests;

internal sealed class BoxingTests
{
    [Test]
    public async Task Should_box_value_as_requested_interface()
    {
        var value = 42;

        var boxed = global::TedToolkit.Annotations.Boxing.Boxing.Box(value);
        var comparable = global::TedToolkit.Annotations.Boxing.Boxing.Box<IComparable, int>(value);

        await Assert.That(boxed).IsTypeOf<int>();
        await Assert.That(comparable.CompareTo(value)).IsEqualTo(0);
    }

    [Test]
    public async Task Should_propagate_non_null_state_from_nullable_input()
    {
        int? value = 42;

        if (value is not null)
        {
            IComparable comparable = global::TedToolkit.Annotations.Boxing.Boxing.Box<IComparable, int>(value);
            await Assert.That(comparable.CompareTo(value.Value)).IsEqualTo(0);
        }
    }

    [Test]
    public async Task Should_preserve_boxing_semantics()
    {
        int? present = 42;
        int? missing = null;

        await Assert.That(global::TedToolkit.Annotations.Boxing.Boxing.Box(present)).IsTypeOf<int>();
        await Assert.That(global::TedToolkit.Annotations.Boxing.Boxing.Box(missing)).IsNull();
    }
}
