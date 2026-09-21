using NUnit.Framework;

namespace Voxta.Unity.Tests
{
    public sealed class VoxtaMicrophoneTests
    {
        [Test]
        public void Pcm16ConversionClampsAndUsesLittleEndian()
        {
            var bytes = Pcm16Conversion.ToLittleEndianPcm16(new[] { -1f, -0.5f, 0f, 0.5f, 1f });
            Assert.That(bytes, Is.EqualTo(new byte[] { 0, 128, 0, 192, 0, 0, 0, 64, 255, 127 }));
        }

        [Test]
        public void RmsUsesTheInputSamples()
        {
            Assert.That(Pcm16Conversion.CalculateRms(new[] { 0f, 1f, 0f, -1f }), Is.EqualTo(0.7071067f).Within(0.00001f));
        }
    }
}
