using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using MedRecPro.DataAccess;
using MedRecPro.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using c = MedRecPro.Models.Constant;
using sc = MedRecPro.Service.ParsingServices.SplConstants; // Constant class for SPL elements and attributes
using static MedRecPro.Models.Label;

namespace MedRecPro.Service.ParsingServices
{
    #region Core Interfaces and Context Objects

    /// <summary>
    /// Defines the contract for a parser that handles a specific section/element of an SPL XML file.
    /// </summary>
    public interface ISplSectionParser
    {
        string SectionName { get; }
        Task<SplParseResult> ParseAsync(XElement element, SplParseContext context);
    }

    /// <summary>
    /// Carries shared state and dependencies throughout the parsing process for a single XML file.
    /// </summary>
    public class SplParseContext
    {
        public IServiceProvider ServiceProvider { get; set; }
        public ILogger Logger { get; set; }
        public SplFileImportResult FileResult { get; set; }
        public string FileNameInZip { get; set; }
        public Document? Document { get; set; }
        public StructuredBody? StructuredBody { get; set; }
        public Section? CurrentSection { get; set; }
        public Product? CurrentProduct { get; set; }
        public int IngredientsCreated { get; set; }

        /// <summary>
        /// Resolves a repository instance for the specified entity type.
        /// </summary>
        public Repository<T> GetRepository<T>() where T : class
        {
            var repo = ServiceProvider.GetService<Repository<T>>();

            if (repo == null)
                throw new InvalidOperationException($"Could not resolve repository for type Repository<{typeof(T).Name}>. Ensure it and its dependencies are registered.");

            return repo;
        }

        public void UpdateFileResult(SplParseResult parseResult)
        {
            FileResult.DocumentsCreated += parseResult.DocumentsCreated;
            FileResult.OrganizationsCreated += parseResult.OrganizationsCreated;
            FileResult.ProductsCreated += parseResult.ProductsCreated;
            FileResult.SectionsCreated += parseResult.SectionsCreated;
            FileResult.Errors.AddRange(parseResult.Errors);

            if (!parseResult.Success)
                FileResult.Success = false;
        }
    }

    /// <summary>
    /// Represents the outcome of a single parsing operation by an ISplSectionParser.
    /// </summary>
    public class SplParseResult
    {
        public bool Success { get; set; } = true;
        public List<string> Errors { get; set; } = new();
        public object? ParsedEntity { get; set; }
        public int DocumentsCreated { get; set; }
        public int OrganizationsCreated { get; set; }
        public int ProductsCreated { get; set; }
        public int SectionsCreated { get; set; }
        public Product? CurrentProduct { get; set; }
        public int IngredientsCreated { get; set; }

        public void MergeFrom(SplParseResult other)
        {
            if (!other.Success) Success = false;
            Errors.AddRange(other.Errors);
            DocumentsCreated += other.DocumentsCreated;
            OrganizationsCreated += other.OrganizationsCreated;
            ProductsCreated += other.ProductsCreated;
            SectionsCreated += other.SectionsCreated;
            IngredientsCreated += other.IngredientsCreated;
        }
    }

    #endregion

}
