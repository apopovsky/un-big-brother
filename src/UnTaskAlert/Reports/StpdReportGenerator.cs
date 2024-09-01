using System.Collections;
using System.Reflection;
using System.Text;
using Flurl;
using UnTaskAlert.Models;

namespace UnTaskAlert.Reports;

/// <summary>
/// A stupid Html report generator because nothing else works inside an Azure function
/// </summary>
public class StpdReportGenerator(string devOpsAddress)
{
    public string GenerateReport(TimeReport timeReport)
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "UnTaskAlert.Reports.DetailReport.cshtml";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
                
        return ProcessReplacements(content, timeReport);
    }

    private string ProcessReplacements(string content, TimeReport timeReport)
    {
        var properties = timeReport.GetType().GetProperties();
        foreach (var property in properties)
        {
            var itemKey = $"@Model.{property.Name}";
            if (property.PropertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(property.PropertyType))
            {
                var startPosition = content.IndexOf(itemKey, StringComparison.Ordinal);
                if (startPosition >= 0)
                {
                    var blockStart = content.IndexOf('{', startPosition);
                    var blockEnd = content.IndexOf('}', blockStart);
                    var template = content.Substring(blockStart + 1, blockEnd - blockStart - 1);
                    var stringBuilder = new StringBuilder();

                    var values = (IEnumerable)property.GetValue(timeReport);
                    foreach (var row in values)
                    {
                        var rowProperties = row.GetType().GetProperties();
                        var rowContent = ""+template;
                        foreach (var rowProp in rowProperties)
                        {
                            var rowItemKey = $"@row.{rowProp.Name}";
                            var propValue = rowProp.GetValue(row);
                            rowContent = rowContent.Replace(rowItemKey, GetFormatterPropertyValue(rowProp, propValue));

                            if (rowProp.Name.Contains("offset", StringComparison.OrdinalIgnoreCase))
                            {
                                var offset = (double) propValue;
                                var color = offset switch
                                {
                                    > .25 and <= .75 => "#ffffe6",
                                    > .75 => "#ffcccc",
                                    _ => "#ffffff",
                                };
                                rowContent = rowContent.Replace("@row.Color", color);
                                    
                            }
                        }
                        stringBuilder.AppendLine(rowContent);
                    }

                    var newRowContent = stringBuilder.ToString();
                    content=content.Remove(startPosition, blockEnd - startPosition + 1);
                    content=content.Insert(startPosition, newRowContent);
                }
            }
            else
            {
                content = content.Replace(itemKey, GetFormatterPropertyValue(property, property.GetValue(timeReport)));
            }
        }

        return content;
    }

    private string GetFormatterPropertyValue(PropertyInfo property, object propValue)
    {
        var baseUrl = new Url(devOpsAddress).AppendPathSegment("/_workitems/edit/");
        if (property.PropertyType == typeof(double))
        {
            var format = "F2";
            if (property.Name.Contains("offset", StringComparison.OrdinalIgnoreCase))
            {
                format = "P1";
            }
            return ((double) propValue).ToString(format);
        }

        if (property.PropertyType == typeof(DateTime))
        {
            return ((DateTime)propValue).ToString("dd/MM/yyyy");
        }

        if (property.Name.Equals("id", StringComparison.OrdinalIgnoreCase))
        {
            var link = $"<a target=\"_blank\" href=\"{baseUrl.AppendPathSegment(propValue)}\">{propValue}</a>";
            return link;
        }
        return propValue?.ToString();
    }
}