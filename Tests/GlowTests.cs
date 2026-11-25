namespace ProjectPluto.Tests
{
    public static class GlowTests
    {
        public static void Glow_DarkWhenCool()
        {
            PlutoGlow.ColorForTemp(400, out double r, out double g, out double b);
            Assert.Equal(0, r + g + b, 1e-9, "no glow below ~500 °C");
        }

        public static void Glow_DullRedAt700()
        {
            PlutoGlow.ColorForTemp(700, out double r, out double g, out double b);
            Assert.True(r > 0 && r > g && g >= b, "red dominates");
        }

        public static void Glow_OrangeAt1100()
        {
            PlutoGlow.ColorForTemp(1100, out double r, out double g, out double b);
            Assert.True(r > g && g > b && g > 0.25 * r, "orange: red over green over blue");
        }

        public static void Glow_BrighterWhenHotter()
        {
            PlutoGlow.ColorForTemp(800, out double r1, out _, out _);
            PlutoGlow.ColorForTemp(1200, out double r2, out _, out _);
            Assert.True(r2 > r1, "brighter");
        }

        public static void Glow_CappedAtOne()
        {
            PlutoGlow.ColorForTemp(5000, out double r, out double g, out double b);
            Assert.True(r <= 1 && g <= 1 && b <= 1, "within 0..1");
        }
    }
}
