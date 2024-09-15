using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CodegenBot;
using HtmlAgilityPack;
using Humanizer;

namespace Pinegrow2Razor;

public class Pinegrow2RazorMiniBot() : IMiniBot
{
    public void Execute()
    {
        var configuration = GraphQLOperations.GetConfiguration();

        var pinegrowProjectFiles = GraphQLOperations.GetFiles(["**/pinegrow.json"], []);

        foreach (var pinegrowProjectFile in pinegrowProjectFiles.Files ?? [])
        {
            var pinegrowProjectDirectory = Path.GetDirectoryName(pinegrowProjectFile.Path)!;
            
            var files = GraphQLOperations.GetFiles([Path.Combine(pinegrowProjectDirectory, "**/*.html").Replace("\\", "/")], []).Files;

            foreach (var file in files ?? [])
            {
                if (file.Kind != FileKind.TEXT)
                {
                    continue;
                }
                
                var content = GraphQLOperations.GetFileContents(file.Path).ReadTextFile;

                if (content is null)
                {
                    continue;
                }
                
                // Using HtmlAgilityPack to check if content is a full HTML document or a partial component
                var isPartial = false;

                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(content.Replace("@", "@@"));
                var htmlNode = htmlDoc.DocumentNode.SelectSingleNode("//html");
                
                if (htmlNode is null)
                {
                    isPartial = true;
                }
                
                RefactorForms(htmlDoc);
                RefactorCheckboxes(htmlDoc);
                FindAndExportComponentDefinitions(htmlDoc, configuration);

                string newContent;

                var razorRoot = htmlDoc.DocumentNode.SelectSingleNode("//html/body");
                if (razorRoot is null)
                {
                    razorRoot = htmlDoc.DocumentNode.SelectSingleNode("//html");
                }
                if (razorRoot is not null)
                {
                    newContent = razorRoot.InnerHtml;
                }
                else
                {
                    newContent = htmlDoc.DocumentNode.OuterHtml;
                }
                
                // Properly format the HTML content using HtmlAgilityPack
                var formattedDoc = new HtmlAgilityPack.HtmlDocument();
                formattedDoc.LoadHtml(newContent);
                using (var stringWriter = new StringWriter())
                {
                    formattedDoc.OptionOutputAsXml = true; // optional, to pretty-print as XML
                    formattedDoc.Save(stringWriter);
                    newContent = stringWriter.ToString().Trim(); // remove leading/trailing whitespace
                    if (newContent.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?><span>"))
                    {
                        newContent = newContent.Substring("<?xml version=\"1.0\" encoding=\"utf-8\"?><span>".Length);
                    }

                    if (newContent.EndsWith("</span>"))
                    {
                        newContent = newContent.Substring(0, newContent.Length - "</span>".Length);
                    }
                    
                    var lines = newContent.Split('\n');
                    var minIndentation = lines
                        .Where(line => !string.IsNullOrWhiteSpace(line))
                        .Select(line => line.TakeWhile(char.IsWhiteSpace).Count())
                        .DefaultIfEmpty(0)
                        .Min();

                    newContent = string.Join("\n", lines.Select(line => line.Length >= minIndentation
                        ? line.Substring(minIndentation)
                        : line));
                }

                newContent = newContent
                    .Replace("&amp;apos;", "'")
                    .Replace("&copy;", "\u00a9");
                
                var newPath = Path.Combine(isPartial ? configuration.Configuration.ComponentDirectory : configuration.Configuration.PageDirectory, Path.GetRelativePath(pinegrowProjectDirectory, file.Path));
                newPath = Path.Combine(Path.GetDirectoryName(newPath)!, Path.GetFileNameWithoutExtension(newPath).Pascalize() + ".razor");

                Imports.Log(new LogEvent()
                {
                    Level = LogEventLevel.Information,
                    Message = "Found {InputFile}, generating {OutputFile}",
                    Args = [file.Path, newPath]
                });

                if (isPartial)
                {
                    if (configuration.Configuration.TreatPartialsAsComponents == true)
                    {
                        GraphQLOperations.AddFile(
                            newPath,
                            $$"""
                              {{newContent}}

                              @code {

                              }

                              """
                        );
                    }
                }
                else
                {
                    var url = Path.GetRelativePath(pinegrowProjectDirectory, file.Path);
                    if (url.EndsWith(".html"))
                    {
                        url = url.Substring(0, url.Length - ".html".Length);
                    }
                    
                    url = "/" + string.Join("/", url.Trim('/').Split('/').Select(part => part.Kebaberize()));
                    
                    GraphQLOperations.AddFile(
                        newPath,
                        $$"""
                          @layout EmptyLayout
                          @page "{{url}}"
                          
                          {{newContent}}

                          @code {

                          }

                          """
                    );
                }
            }
        }
    }

    private static void FindAndExportComponentDefinitions(HtmlDocument htmlDoc, GetConfigurationData configuration)
    {
        // Find elements with the "data-pgc-define" attribute
        var componentElements = htmlDoc.DocumentNode.SelectNodes("//*[@data-pgc-define]")?.AsEnumerable() ?? Enumerable.Empty<HtmlNode>();

        foreach (var componentElement in componentElements)
        {
            var componentId = componentElement.GetAttributeValue("data-pgc-define", "");

            var componentName = componentElement.GetAttributeValue("data-pgc-define-name", "");

            componentElement.Attributes.Remove("data-pgc-define");
            if (componentElement.Attributes.Contains("data-pgc-define-name"))
            {
                componentElement.Attributes.Remove("data-pgc-define-name");
            }

            var razorFileName = componentName?.Pascalize();
            if (string.IsNullOrWhiteSpace(razorFileName))
            {
                razorFileName = componentId.Pascalize();
            }

            var outputPath = Path.Combine(configuration.Configuration.ComponentDirectory, $"{razorFileName}.razor");

            GraphQLOperations.AddFile(outputPath, 
                $$"""
                  {{CaretRef.New(out var html)}}
                  
                  @code {
                  {{CaretRef.New(out var parameters)}}
                  }
                  """);

            var parameterAttributes = new StringBuilder();
            
            AddStructureForEditable("", componentElement, parameterAttributes, parameters);
            
            if (componentElement.ParentNode is not null)
            {
                componentElement.ParentNode.ReplaceChild(HtmlNode.CreateNode($"<{razorFileName}{parameterAttributes}></{razorFileName}>", x => x.OptionOutputOriginalCase = true), componentElement);
            }
            
            var componentOuterHtml = componentElement.OuterHtml;
            GraphQLOperations.AddText(html.Id, componentOuterHtml);

            Imports.Log(new LogEvent()
            {
                Level = LogEventLevel.Information,
                Message = "Generated component {OutputFile}",
                Args = [outputPath]
            });
        }
    }

    private static void AddStructureForEditable(string path, HtmlNode element, StringBuilder? parameterAttributes, CaretRef parameters)
    {
        var editValue = element.GetAttributeValue("data-pgc-edit", "");
        if (!string.IsNullOrWhiteSpace(editValue))
        {
            var parts = editValue.Split(new[] { '[', ']' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 2)
            {
                var parameterName = parts[0];
                var parameterTargets = parts[1].Split(',').Select(target => target.Trim()).ToList();
                if (parameterTargets.Any(target => target == "no_content"))
                {
                    parameterTargets.Add("content");
                }
                else
                {
                    parameterTargets.Remove("no_content");
                }

                string value;

                foreach (var parameterTarget in parameterTargets)
                {
                    if (parameterTarget == "content")
                    {
                        // Assume it's an inner HTML binding
                        value = element.InnerHtml;
                        element.InnerHtml = $"@{parameterName.Pascalize()}";
                        if (element.HasChildNodes)
                        {
                            GraphQLOperations.AddText(parameters.Id,
                                $$"""
                                      [Parameter]
                                      public RenderFragment {{parameterName.Pascalize()}} { get; set; }
                                      
                                  """);
                        }
                        else
                        {
                            GraphQLOperations.AddText(parameters.Id,
                                $$"""
                                      [Parameter]
                                      public string {{parameterName.Pascalize()}} { get; set; }
                                      
                                  """);
                        }
                    }
                    else
                    {
                        // Assume it's an attribute binding, use a markup extension
                        var attributeName = parameterTarget;
                        value = element.GetAttributeValue(attributeName, "");

                        var attributeParameterName = parameterName;
                        
                        if (parameterTargets.Any(target => target == "content"))
                        {
                            attributeParameterName += $" {attributeName}";
                        }
                        
                        element.SetAttributeValue(attributeName, $"@{attributeParameterName.Pascalize()}");

                        GraphQLOperations.AddText(parameters.Id,
                            $$"""
                                  [Parameter]
                                  public string {{attributeParameterName.Pascalize()}} { get; set; }
                                  
                              """);
                    }
                    
                    if (value.Any(x => x == '\n'))
                    {
                        //parameterAttributes.Append($" {parameterName}=\"@{parameterName.Pascalize()}\"");
                    }
                    else
                    {
                        //parameterAttributes.Append($" {parameterName}=\"{value}\"");
                    }
                }
            }
        }
        
        var repeats = new Dictionary<string, CaretRef>();
        
        foreach (var editableElement in element.ChildNodes?.OfType<HtmlNode>() ?? [])
        {
            var repeatableWith = editableElement.GetAttributeValue("data-pgc-repeat", "");
            if (repeatableWith != "")
            {
                if (!repeats.ContainsKey(repeatableWith))
                {
                    GraphQLOperations.AddText(parameters.Id,
                        $$"""
                              public List<object> {{repeatableWith.Pascalize()}} { get; set; }
                              
                              public class {{repeatableWith.Pascalize()}}Item
                              {
                              {{CaretRef.New(out var innerParameters)}}
                              }
                              
                          """);
                    repeats.Add(repeatableWith, innerParameters);
                }
                AddStructureForEditable(path + repeatableWith.Pascalize(), editableElement, null, repeats[repeatableWith]);
            }
            else
            {
                AddStructureForEditable(path, editableElement, null, parameters);
            }
        }
    }

    private static void RefactorForms(HtmlDocument htmlDoc)
    {
        var forms = htmlDoc.DocumentNode?.SelectNodes("form")?.AsEnumerable() ?? [];
        foreach (var form in forms)
        {
            var attributes = new StringBuilder();
            foreach (var attribute in form.GetAttributes())
            {
                attributes.Append(" ");
                attributes.Append(attribute);
            }

            form.ParentNode?.ReplaceChild(HtmlNode.CreateNode($"<EditForm{attributes}>{form.InnerHtml}</EditForm>", x => x.OptionOutputOriginalCase = true), form);
        }
    }
    
    private static void RefactorCheckboxes(HtmlDocument htmlDoc)
    {
        var checkboxes = htmlDoc.DocumentNode.SelectNodes("//input[@type='checkbox']")?.AsEnumerable() ?? [];
        foreach (var checkbox in checkboxes)
        {
            var attributes = new StringBuilder();
            foreach (var attribute in checkbox.GetAttributes())
            {
                if (attribute.Name is "type")
                {
                    continue;
                }
                attributes.Append(" ");
                attributes.Append(attribute);
            }
            checkbox.ParentNode.ReplaceChild(HtmlNode.CreateNode($"<InputCheckbox{attributes}>{checkbox.InnerHtml}</InputCheckbox>", x => x.OptionOutputOriginalCase = true), checkbox);
        }
    }
}
