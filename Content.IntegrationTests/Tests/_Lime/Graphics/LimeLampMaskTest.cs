using Content.IntegrationTests.Fixtures;
using Content.Shared._Lime.Shaders.Bloom;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Lime.Graphics;

/// <summary>Checks that lamp masks use the texture-relative SpriteSpecifier contract.</summary>
public sealed class LimeLampMaskTest : GameTest
{
    [TestCase("Poweredlight")]
    [TestCase("PoweredSmallLight")]
    [TestCase("LightPostSmall")]
    [TestCase("EmergencyLight")]
    [TestCase("AlwaysPoweredStrobeLight")]
    public async Task PrototypeMaskResolves(string prototype)
    {
        EntityUid lamp = default;
        await Client.WaitPost(() => lamp = CEntMan.SpawnEntity(new EntProtoId(prototype), MapCoordinates.Nullspace));
        try
        {
            await Client.WaitAssertion(() =>
            {
                var mask = CEntMan.GetComponent<LimeLampBloomComponent>(lamp).MaskSprite;
                // Frame0 добавляет /Textures; GetFrame ошибочно загружал относительный путь как полный.
                var texture = CEntMan.System<SpriteSystem>().Frame0(mask);
                Assert.That(texture.Size.X, Is.GreaterThan(0));
                Assert.That(texture.Size.Y, Is.GreaterThan(0));
            });
        }
        finally
        {
            await Client.WaitPost(() => CEntMan.DeleteEntity(lamp));
        }
    }
}
