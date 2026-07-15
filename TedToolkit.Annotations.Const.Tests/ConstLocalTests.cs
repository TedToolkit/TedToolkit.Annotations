using TedToolkit.Annotations.Const;

namespace TedToolkit.Annotations.Const.Tests;

internal sealed class ConstLocalTests
{
    [Test]
    public async Task Should_preserve_storage_identity_for_ref_local()
    {
        var value = 1;

        ref var local = ref global::TedToolkit.Annotations.Const.Const.Local(ref value);
        local = 2;

        await Assert.That(value).IsEqualTo(2);
    }

    [Test]
    public async Task Should_preserve_value_and_reference_identity()
    {
        var value = new object();
        var local = global::TedToolkit.Annotations.Const.Const.Local(value, ConstDepth.DEPTH1_OR_GREATER);
        var number = 1;
        ref var alias = ref global::TedToolkit.Annotations.Const.Const.Local(ref number);
        alias = 2;

        await Assert.That(local).IsSameReferenceAs(value);
        await Assert.That(number).IsEqualTo(2);
    }
}
