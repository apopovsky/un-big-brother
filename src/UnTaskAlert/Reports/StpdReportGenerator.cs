using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Flurl;
using Telegram.Bot.Types.InputFiles;
using UnTaskAlert.Models;

namespace UnTaskAlert.Reports
{
	/// <summary>
	/// A stupid Html report generator because nothing else works inside an Azure function
	/// </summary>
	public class StpdReportGenerator
	{
		private string _devOpsAddress;

		public StpdReportGenerator(string devOpsAddress)
		{
			_devOpsAddress = devOpsAddress;
		}

		public string GenerateReport(TimeReport timeReport)
		{
			var assembly = Assembly.GetExecutingAssembly();
			var resourceName = "UnTaskAlert.Reports.DetailReport.cshtml";

			using (Stream stream = assembly.GetManifestResourceStream(resourceName))
			using (StreamReader reader = new StreamReader(stream))
			{
				var content = reader.ReadToEnd();
				
				return ProcessReplacements(content, timeReport);
			}
;
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

						var values = property.GetValue(timeReport) as IEnumerable;
						foreach (var row in values)
						{
							var rowProperties = row.GetType().GetProperties();
							var rowContent = ""+template;
							foreach (var rowProp in rowProperties)
							{
								var rowItemKey = $"@row.{rowProp.Name}";
								rowContent = rowContent.Replace(rowItemKey, GetFormatterPropertyValue(rowProp, row));
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
					content = content.Replace(itemKey, GetFormatterPropertyValue(property, timeReport));
				}
			}

			return content;
		}

		private string GetFormatterPropertyValue(PropertyInfo property, object @object)
		{
			var baseUrl = new Url(_devOpsAddress).AppendPathSegment("/_workitems/edit/");
			var value = property.GetValue(@object);
			if (property.PropertyType == typeof(double))
			{
				var format = "F2";
				if (property.Name.Contains("offset", StringComparison.OrdinalIgnoreCase))
				{
					format = "P1";
				}
				return ((double) value).ToString(format);
			}
			else
			{
				if (property.Name.Equals("id", StringComparison.OrdinalIgnoreCase))
				{
					var link = $"<a target=\"_blank\" href=\"{baseUrl.AppendPathSegment(value)}\">{value}</a>";
					return link;
				}
				return value?.ToString();
			}
		}
	}
}
