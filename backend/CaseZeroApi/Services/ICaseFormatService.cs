using CaseZeroApi.Models;

namespace CaseZeroApi.Services
{
    /// <summary>
    /// Service for converting between different case formats
    /// </summary>
    public interface ICaseFormatService
    {
        /// <summary>
        /// Convert a NormalizedCaseBundle (from CaseGen) to CaseObject (game format)
        /// </summary>
        /// <param name="normalizedCaseJson">JSON string containing NormalizedCaseBundle</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>CaseObject in game-consumable format</returns>
        Task<CaseObject> ConvertToGameFormatAsync(string normalizedCaseJson, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Convert a NormalizedCaseBundle object to CaseObject (game format)
        /// </summary>
        /// <param name="normalizedBundle">NormalizedCaseBundle object</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>CaseObject in game-consumable format</returns>
        Task<CaseObject> ConvertToGameFormatAsync(object normalizedBundle, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Validate that a normalized case bundle can be converted to game format
        /// </summary>
        /// <param name="normalizedCaseJson">JSON string containing NormalizedCaseBundle</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Validation result with any issues found</returns>
        Task<FormatValidationResult> ValidateForGameFormatAsync(string normalizedCaseJson, CancellationToken cancellationToken = default);
    }
    
    /// <summary>
    /// Result of format validation
    /// </summary>
    public class FormatValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Issues { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }
}