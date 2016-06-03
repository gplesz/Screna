using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Ninject;
using Screna.Audio;

namespace Screna.Tests
{
    [TestClass]
    public class SetUp
    {
        static readonly IKernel Kernel = new StandardKernel();

        [AssemblyInitialize]
        public static void Initialize(TestContext TestContext)
        {
            Kernel.Bind<IAudioFileWriter>().ToConstant(new Mock<IAudioFileWriter>().Object);
            Kernel.Bind<IAudioProvider>().ToConstant(new Mock<IAudioProvider>().Object);

            InitImageProvider();

            Kernel.Bind<IVideoFileWriter>().ToConstant(new Mock<IVideoFileWriter>().Object);
        }

        static void InitImageProvider()
        {
            const int originalWidth = 3, originalHeight = 4;

            var imageProvider = new Mock<IImageProvider>();

            imageProvider.Setup(X => X.Capture()).Returns(() => new Bitmap(originalWidth, originalHeight));

            imageProvider.Setup(X => X.Width).Returns(originalWidth);
            imageProvider.Setup(X => X.Height).Returns(originalHeight);

            Kernel.Bind<IImageProvider>().ToConstant(imageProvider.Object);
        }

        public static T Get<T>() => Kernel.Get<T>();
    }
}