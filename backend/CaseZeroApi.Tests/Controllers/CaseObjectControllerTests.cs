using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using CaseZeroApi.Controllers;
using CaseZeroApi.Services;

namespace CaseZeroApi.Tests.Controllers
{
    public class CaseObjectControllerTests
    {
        private readonly Mock<ICaseObjectService> _mockCaseObjectService;
        private readonly Mock<ILogger<CaseObjectController>> _mockLogger;
        private readonly CaseObjectController _controller;

        public CaseObjectControllerTests()
        {
            _mockCaseObjectService = new Mock<ICaseObjectService>();
            _mockLogger = new Mock<ILogger<CaseObjectController>>();
            _controller = new CaseObjectController(_mockCaseObjectService.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task GetAvailableCases_ReturnsListOfCases()
        {
            // Arrange
            var expectedCases = new List<string> { "Case001", "Case002" };
            _mockCaseObjectService.Setup(x => x.GetAvailableCasesAsync())
                .ReturnsAsync(expectedCases);

            // Act
            var result = await _controller.GetAvailableCases();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var cases = Assert.IsType<List<string>>(okResult.Value);
            Assert.Equal(expectedCases.Count, cases.Count);
            Assert.Equal(expectedCases, cases);
        }

        [Fact]
        public async Task GetAvailableCases_ServiceThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            _mockCaseObjectService.Setup(x => x.GetAvailableCasesAsync())
                .ThrowsAsync(new Exception("Service error"));

            // Act
            var result = await _controller.GetAvailableCases();

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(500, statusCodeResult.StatusCode);
        }
    }
}