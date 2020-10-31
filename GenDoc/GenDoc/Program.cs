﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GenDoc
{
    internal enum GenDocType
    {
        LuaFunctionsTableArray,
        LuaEventsArray,
        LuaNetPropsTableArray,
    }

    public class Admonition
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("data")]
        public string Data { get; set; }
    }

    public class LuaFunctionArgument
    {
        [JsonProperty("alias")]
        public string Alias { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("optional")]
        public bool Optional { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }
    }

    public class LuaFunctionReturn
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }
    }

    public class LuaFunction
    {
        [JsonProperty("alias")]
        public string Alias { get; set; }

        [JsonProperty("arguments")]
        public List<LuaFunctionArgument> Arguments { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("returns")]
        public List<LuaFunctionReturn> Returns { get; set; }

        [JsonProperty("admonitions")]
        public List<Admonition> Admonitions { get; set; }
    }

    public class LuaFunctionsTable
    {
        [JsonProperty("alias")]
        public string Alias { get; set; }

        [JsonProperty("functions")]
        public List<LuaFunction> Functions { get; set; }
    }

    public class Root
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("MyArray")]
        public JArray MyArray { get; set; }
    }

    public static class Helper
    {
        public static string GetFullPathWithoutExtension(string path)
        {
            return Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, Path.GetFileNameWithoutExtension(path));
        }

        public static List<string> GetFilesDeep(string root, string regex, int depth)
        {
            var files = new List<string>();

            foreach (var directory in Directory.EnumerateDirectories(root))
            {
                if (depth > 0)
                    files.AddRange(GetFilesDeep(directory, regex, depth - 1));
            }

            var filesFound = Directory.EnumerateFiles(root);

            foreach (var fileFound in filesFound)
            {
                if (Regex.Match(fileFound, regex).Success)
                    files.Add(fileFound);
            }

            return files;
        }
    }

    internal static class GenDoc
    {
        private static readonly Dictionary<GenDocType, Action<StringBuilder, Root>> TypeToAction =
            new Dictionary<GenDocType, Action<StringBuilder, Root>>
            {
                {
                    GenDocType.LuaFunctionsTableArray,
                    (stringBuilder, jsonMapRoot) =>
                    {
                        var jsonMapRootCastMyArray = jsonMapRoot.MyArray.ToObject<List<LuaFunctionsTable>>();

                        if (jsonMapRootCastMyArray == null)
                            return;

                        foreach (var luaFunctionsTable in jsonMapRootCastMyArray)
                        {
                            stringBuilder.AppendLine($"## {luaFunctionsTable.Alias}\n\n---\n");

                            if (luaFunctionsTable.Functions == null) continue;

                            foreach (var luaFunction in luaFunctionsTable.Functions)
                            {
                                stringBuilder.AppendLine($"### {luaFunction.Alias}\n");

                                if (!string.IsNullOrEmpty(luaFunction.Description))
                                    stringBuilder.AppendLine($"{luaFunction.Description}\n");

                                stringBuilder.Append(
                                    $"`{luaFunctionsTable.Alias}{(luaFunctionsTable.Alias.Contains("{}") ? ":" : ".")}{luaFunction.Alias}(");

                                var argumentsExists = luaFunction.Arguments != null;
                                var returnsExists = luaFunction.Returns != null;

                                /*
                                 * Write arguments
                                 */
                                if (argumentsExists)
                                {
                                    var isReadingOptionalArguments = false;
                                    var optionalArgumentsToClose = 0;

                                    foreach (var luaFunctionArgument in luaFunction.Arguments)
                                    {
                                        if (luaFunction.Arguments.IndexOf(luaFunctionArgument) == 0)
                                        {
                                            stringBuilder.Append(
                                                $"{luaFunctionArgument.Alias}: {luaFunctionArgument.Type}");
                                        }
                                        else
                                        {
                                            if (luaFunctionArgument.Optional)
                                            {
                                                isReadingOptionalArguments = true;
                                                optionalArgumentsToClose++;
                                            }

                                            stringBuilder.Append(isReadingOptionalArguments
                                                ? $" [, {luaFunctionArgument.Alias}: {luaFunctionArgument.Type}"
                                                : $", {luaFunctionArgument.Alias}: {luaFunctionArgument.Type}");
                                        }
                                    }

                                    for (var i = 0; i < optionalArgumentsToClose; i++)
                                    {
                                        stringBuilder.Append(']');
                                    }
                                }

                                stringBuilder.Append(')');

                                /*
                                 * Write returns
                                 */
                                if (returnsExists)
                                {
                                    if (luaFunction.Returns?.Count > 0)
                                        stringBuilder.Append(" : ");


                                    foreach (var luaFunctionReturn in luaFunction.Returns)
                                    {
                                        stringBuilder.Append(luaFunction.Returns.IndexOf(luaFunctionReturn) == 0
                                            ? $"{luaFunctionReturn.Type}"
                                            : $", {luaFunctionReturn.Type}");
                                    }
                                }

                                stringBuilder.AppendLine("`\n");

                                /*
                                 * Write table of arguments
                                 */
                                if (argumentsExists && luaFunction.Arguments.Count > 0)
                                {
                                    stringBuilder.AppendLine("|Argument|Type|Optional|Description|");
                                    stringBuilder.AppendLine("|-|-|-|-|");

                                    foreach (var luaFunctionArgument in luaFunction.Arguments)
                                    {
                                        stringBuilder.AppendLine(
                                            $"|{luaFunctionArgument.Alias}|{luaFunctionArgument.Type}|{(luaFunctionArgument.Optional ? "Yes" : "No")}|{luaFunctionArgument.Description}|");
                                    }

                                    stringBuilder.Append("\n");
                                }

                                /*
                                 * Write table of returns
                                 */
                                if (returnsExists && luaFunction.Returns.Count > 0)
                                {
                                    stringBuilder.AppendLine("|Return|Type|Description|");
                                    stringBuilder.AppendLine("|-|-|-|");

                                    foreach (var (value, i) in luaFunction.Returns.Select((value, i) => (value, i)))
                                    {
                                        var positionalIndex = (i + 1) switch
                                        {
                                            1 => "1st",
                                            2 => "2nd",
                                            3 => "3rd",
                                            _ => $"{i + 1}th"
                                        };

                                        stringBuilder.AppendLine(
                                            $"|{positionalIndex}|{value.Type}|{value.Description}|");
                                    }

                                    stringBuilder.Append("\n");
                                }

                                /*
                                 * Write admonitions
                                 */

                                if (luaFunction.Admonitions != null && luaFunction.Admonitions.Count > 0)
                                {
                                    foreach (var luaFunctionAdmonition in luaFunction.Admonitions)
                                    {
                                        stringBuilder.AppendLine($"!!! {luaFunctionAdmonition.Type} \"{luaFunctionAdmonition.Title}\"");

                                        stringBuilder.AppendLine($"    {luaFunctionAdmonition.Data}");

                                        stringBuilder.Append("\n");
                                    }
                                }
                            }
                        }
                    }
                },
            };

        private static void ForType(GenDocType type, StringBuilder stringBuilder, Root jsonMapRoot)
        {
            TypeToAction[type](stringBuilder, jsonMapRoot);
        }

        public static void ForFile(string path)
        {
            var pathIn = Path.GetRelativePath(Directory.GetCurrentDirectory(),
                Path.Combine(Directory.GetCurrentDirectory(), path));
            var pathOut = Helper.GetFullPathWithoutExtension(pathIn) + ".md";

            if (!File.Exists(pathIn)) return;

            using var streamReader = new StreamReader(pathIn);
            using var streamWriter = new StreamWriter(pathOut, false, Encoding.UTF8);
            var stringBuilder = new StringBuilder();

            stringBuilder.AppendLine("# " + Path.GetFileNameWithoutExtension(path) + "\n");

            var jsonMapRoot = JsonConvert.DeserializeObject<Root>(streamReader.ReadToEndAsync().Result);

            var docType = (GenDocType)Enum.Parse(typeof(GenDocType), jsonMapRoot.Type);

            ForType(docType, stringBuilder, jsonMapRoot);

            streamWriter.Write(stringBuilder);
        }
    }

    internal class Program
    {
        private static void Main(string[] args)
        {
            foreach (var filepath in Helper.GetFilesDeep(Path.Combine(Directory.GetCurrentDirectory(), args[0]), ".*\\.json$", 10))
            {
                GenDoc.ForFile(filepath);
            }
        }
    }
}