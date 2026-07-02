using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Exercises <see cref="ViewRenderService.RenderToStringAsync"/> using a
    /// mocked Razor view engine so no physical .cshtml views are required.
    /// </summary>
    /// <remarks>
    /// The found-view path uses a Moq <see cref="IView"/> whose RenderAsync
    /// writes through the ViewContext writer, proving the service pipes the
    /// model and writer correctly. The missing-view path asserts the
    /// documented ArgumentNullException.
    /// </remarks>
    /// <seealso cref="ViewRenderService"/>
    /// <seealso cref="IViewRenderService"/>
    [TestClass]
    public class ViewRenderServiceTests
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Verifies a found view renders through the ViewContext writer and
        /// receives the supplied model.
        /// </summary>
        /// <seealso cref="ViewRenderService.RenderToStringAsync"/>
        [TestMethod]
        public async Task ViewRenderService_RenderToStringAsync_FoundView_ReturnsRenderedText()
        {
            #region implementation
            // Arrange - fake view writes a marker plus the model value.
            var view = new Mock<IView>();
            view.Setup(v => v.RenderAsync(It.IsAny<ViewContext>()))
                .Returns<ViewContext>(viewContext =>
                    viewContext.Writer.WriteAsync($"RENDERED:{viewContext.ViewData.Model}"));

            var viewEngine = new Mock<IRazorViewEngine>();
            viewEngine.Setup(e => e.FindView(It.IsAny<ActionContext>(), "TestView", false))
                .Returns(ViewEngineResult.Found("TestView", view.Object));

            var service = new ViewRenderService(
                viewEngine.Object,
                new Mock<ITempDataProvider>().Object,
                new ServiceCollection().BuildServiceProvider());

            // Act
            var rendered = await service.RenderToStringAsync("TestView", "fixture-model");

            // Assert
            Assert.AreEqual("RENDERED:fixture-model", rendered);
            view.Verify(v => v.RenderAsync(It.IsAny<ViewContext>()), Times.Once);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies a missing view produces the documented
        /// ArgumentNullException naming the view.
        /// </summary>
        /// <seealso cref="ViewRenderService.RenderToStringAsync"/>
        [TestMethod]
        public async Task ViewRenderService_RenderToStringAsync_MissingView_ThrowsArgumentNullException()
        {
            #region implementation
            // Arrange - engine reports the view as not found anywhere.
            var viewEngine = new Mock<IRazorViewEngine>();
            viewEngine.Setup(e => e.FindView(It.IsAny<ActionContext>(), It.IsAny<string>(), false))
                .Returns(ViewEngineResult.NotFound("MissingView", Array.Empty<string>()));

            var service = new ViewRenderService(
                viewEngine.Object,
                new Mock<ITempDataProvider>().Object,
                new ServiceCollection().BuildServiceProvider());

            // Act + Assert
            var exception = await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                () => service.RenderToStringAsync("MissingView", new object()));

            StringAssert.Contains(exception.Message, "MissingView");
            #endregion
        }

        #endregion
    }
}
