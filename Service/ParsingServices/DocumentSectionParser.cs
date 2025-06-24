// This is the fully refactored DocumentSectionParser.cs

using System;
using System.Threading.Tasks;
using System.Xml.Linq;
using MedRecPro.Models;
using Microsoft.Extensions.Logging;
using static MedRecPro.Models.Label;
using MedRecPro.Helpers; 
using sc = MedRecPro.Service.ParsingServices.SplConstants; // Constant class for spl elements and attributes
using c = MedRecPro.Models.Constant; //  Constant class for other constants

namespace MedRecPro.Service.ParsingServices
{
    public class DocumentSectionParser : ISplSectionParser
    {
        public string SectionName => sc.E.Document;
        private static readonly XNamespace ns = c.XML_NAMESPACE;

        public async Task<SplParseResult> ParseAsync(XElement element, SplParseContext context)
        {
            var result = new SplParseResult();
            try
            {
                var document = parseDocumentElement(element, context.Logger);
                if (document == null)
                {
                    result.Success = false;
                    result.Errors.Add("Could not parse main document metadata.");
                    return result;
                }

                document.SubmissionFileName = context.FileNameInZip;

                var docRepo = context.GetRepository<Document>();
                await docRepo.CreateAsync(document);

                if (!document.DocumentID.HasValue)
                {
                    throw new InvalidOperationException("DocumentID was not populated by the database after creation.");
                }

                context.Document = document;
                result.DocumentsCreated = 1;
                result.ParsedEntity = document;

                context.Logger.LogInformation("Created Document with ID {DocumentID} for file {FileName}",
                    document.DocumentID, context.FileNameInZip);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Error parsing document: {ex.Message}");
                context.Logger.LogError(ex, "Error parsing document element");
            }
            return result;
        }

        private Document? parseDocumentElement(XElement docEl, ILogger logger)
        {
            try
            {
                // Find the 'code' element once to reuse it
                var ce = docEl.Element(ns + sc.E.Code);

                return new Document
                {
                    DocumentGUID = Util.ParseNullableGuid(docEl.GetChildAttrVal(ns + sc.E.Id, sc.A.Root) ?? string.Empty),

                    DocumentCode = ce?.GetAttrVal(sc.A.CodeValue),

                    DocumentCodeSystem = ce?.GetAttrVal(sc.A.CodeSystem),

                    DocumentDisplayName = ce?.GetAttrVal(sc.A.DisplayName),

                    Title = docEl.GetChildVal(ns + sc.E.Title)?.Trim(),

                    EffectiveTime = Util.ParseNullableDateTime(docEl.GetChildAttrVal(ns + sc.E.EffectiveTime, sc.A.Value) ?? string.Empty),

                    SetGUID = Util.ParseNullableGuid(docEl.GetChildAttrVal(ns + sc.E.SetId, sc.A.Root) ?? string.Empty),

                    VersionNumber = Util.ParseNullableInt(docEl.GetChildAttrVal(ns + sc.E.VersionNumber, sc.A.Value) ?? string.Empty),
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error parsing <document> element attributes.");
                return null;
            }
        }
    }
}