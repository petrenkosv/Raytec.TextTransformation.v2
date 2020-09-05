using System;
using System.Activities;
using System.Threading;
using System.Threading.Tasks;
using System.Data;
using Raytec.TextTransformation.Activities.Properties;
using UiPath.Shared.Activities;
using UiPath.Shared.Activities.Localization;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Text.RegularExpressions;
using System.Globalization;



namespace Raytec.TextTransformation.Activities
{
    [LocalizedDisplayName(nameof(Resources.ContractDataRecognition_DisplayName))]
    [LocalizedDescription(nameof(Resources.ContractDataRecognition_Description))]

    #region KeywordsClass
    public class ParagraphClass
    {
        public List<ParagraphAttributes> paragraph { get; set; }
    }

    public class ParagraphAttributes
    {
        public string name { get; set; }
        public List<string> keys { get; set; }
        public List<SentenceAttributes> sentence { get; set; }
    }

    public class SentenceAttributes
    {
        public string name { get; set; }
        public List<string> keys { get; set; }
        public string format { get; set; }
        public int overlap { get; set; }
        public List<string> options { get; set; }
        public List<string> split { get; set; }
    }
    #endregion

    public class ContractDataRecognition : ContinuableAsyncCodeActivity
    {
        #region Properties

        /// <summary>
        /// If set, continue executing the remaining activities even if the current activity has failed.
        /// </summary>
        [LocalizedCategory(nameof(Resources.Common_Category))]
        [LocalizedDisplayName(nameof(Resources.ContinueOnError_DisplayName))]
        [LocalizedDescription(nameof(Resources.ContinueOnError_Description))]
        public override InArgument<bool> ContinueOnError { get; set; }

        [LocalizedDisplayName(nameof(Resources.ContractDataRecognition_Text_DisplayName))]
        [LocalizedDescription(nameof(Resources.ContractDataRecognition_Text_Description))]
        [LocalizedCategory(nameof(Resources.Input_Category))]
        public InArgument<string> Text { get; set; }

        [LocalizedDisplayName(nameof(Resources.ContractDataRecognition_PathToTheSettingsFile_DisplayName))]
        [LocalizedDescription(nameof(Resources.ContractDataRecognition_PathToTheSettingsFile_Description))]
        [LocalizedCategory(nameof(Resources.Input_Category))]
        public InArgument<string> PathToTheSettingsFile { get; set; }

        [LocalizedDisplayName(nameof(Resources.ContractDataRecognition_LineSeparator_DisplayName))]
        [LocalizedDescription(nameof(Resources.ContractDataRecognition_LineSeparator_Description))]
        [LocalizedCategory(nameof(Resources.Input_Category))]
        public InArgument<string> LineSeparator { get; set; }

        [LocalizedDisplayName(nameof(Resources.ContractDataRecognition_CultureVariable_DisplayName))]
        [LocalizedDescription(nameof(Resources.ContractDataRecognition_CultureVariable_Description))]
        [LocalizedCategory(nameof(Resources.Input_Category))]
        public InArgument<string> CultureVariable { get; set; }

        [LocalizedDisplayName(nameof(Resources.ContractDataRecognition_Output_DisplayName))]
        [LocalizedDescription(nameof(Resources.ContractDataRecognition_Output_Description))]
        [LocalizedCategory(nameof(Resources.Output_Category))]
        public OutArgument<DataTable> Output { get; set; }

        #endregion


        #region Constructors

        public ContractDataRecognition()
        {
        }

        #endregion


        #region Transformators
        // Split the text into paragraphs using the line separator and other newline characters.
        static List<string> GetParagraphs(string inputText, string lineSeparator)
        {
            // Split the text into paragraphs using the line separator and other newline characters
            var splitText = inputText.Split(new[] { lineSeparator, Environment.NewLine, "\r\n", "\n" }, StringSplitOptions.None).ToList();
            var paragraphsText = new List<string>();
            var tempText = new List<string>();
        
            // Initialize the first paragraph
            if (splitText.Count > 0)
            {
                tempText.Add(splitText[0]);
            }
        
            // Iterate through the split text to group lines into paragraphs
            for (int i = 1; i < splitText.Count; i++)
            {
                if (splitText[i].Length > 1)
                {
                    // Check if the line starts a new paragraph (e.g., numbered or dashed lines)
                    if ((Char.IsDigit(splitText[i][0]) && splitText[i][1].Equals('.')) || splitText[i][0].Equals('-'))
                    {
                        if (tempText.Count > 0)
                        {
                            paragraphsText.Add(String.Join(" ", tempText));
                        }
                        tempText = new List<string> { splitText[i] };
                    }
                    else
                    {
                        tempText.Add(splitText[i]);
                    }
                }
        
                // Add the last paragraph if it's not empty
                if (i == splitText.Count - 1 && tempText.Count > 0)
                {
                    paragraphsText.Add(String.Join(" ", tempText));
                }
            }
        
            // Remove the first part of each paragraph that contains numbers, dots, or dashes
            var paragraphSubstring = new List<string>();
            foreach (var item in paragraphsText)
            {
                var charCounter = 0;
        
                // Count characters to skip (numbers, dots, or dashes)
                foreach (var character in item)
                {
                    if (Char.IsDigit(character) || character.Equals('.') || character.Equals('-'))
                    {
                        charCounter++;
                    }
                    else
                    {
                        break;
                    }
                }
        
                // Add the cleaned paragraph to the result
                paragraphSubstring.Add(item.Substring(charCounter).Trim());
            }
        
            return paragraphSubstring;
        }

        // Find the best match for the paragraph using the keywords and return its index.
        static int ParagraphDefinition(List<string> text, ParagraphAttributes paragraph, int overlapNumber = 1)
        {
            // Initialize the best match array to store the index and match count
            var bestMatch = new int[2] { -1, 0 };
        
            // Iterate through each paragraph in the text
            for (int tNum = 0; tNum < text.Count; tNum++)
            {
                // Split the current paragraph into words
                var words = text[tNum].ToLower().Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);
        
                // Create a 2D array to store the number of matches for each keyword in the paragraph
                var cell = new int[paragraph.keys.Count, words.Length];
        
                for (int i = 0; i < paragraph.keys.Count; i++)
                {
                    for (int j = 0; j < words.Length; j++)
                    {
                        // Handle boundary conditions for the 2D array
                        int i2 = i > 0 ? i - 1 : 0;
                        int j2 = j > 0 ? j - 1 : 0;
        
                        // Check if the current word contains the keyword
                        if (words[j].Contains(paragraph.keys[i].ToLower()))
                        {
                            cell[i, j] = cell[i2, j2] + 1;
                        }
                        else
                        {
                            cell[i, j] = Math.Max(cell[i2, j], cell[i, j2]);
                        }
                    }
                }
        
                // Find the maximum match count in the 2D array
                int maxCell = cell.Cast<int>().Max();
        
                // Update the best match if the current match count is higher
                if (bestMatch[1] < maxCell)
                {
                    bestMatch[0] = tNum;
                    bestMatch[1] = maxCell;
                }
            }
        
            // Return the index of the best match if it meets the overlap threshold, otherwise return -1
            return (bestMatch[1] >= overlapNumber) ? bestMatch[0] : -1;
        }

        // Find the best match for the sentence using the keywords and return its index.
        static int SentenceDefinition(List<string> text, SentenceAttributes sentence, int overlapNumber = 1)
        {
            // Initialize the best match array to store the index and match count
            var bestMatch = new int[2] { -1, 0 };
        
            // Iterate through each sentence in the text
            for (int tNum = 0; tNum < text.Count; tNum++)
            {
                // Split the current sentence into words
                var words = text[tNum].ToLower().Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);
        
                // Create a 2D array to store the number of matches for each keyword in the sentence
                var cell = new int[sentence.keys.Count, words.Length];
        
                for (int i = 0; i < sentence.keys.Count; i++)
                {
                    for (int j = 0; j < words.Length; j++)
                    {
                        // Handle boundary conditions for the 2D array
                        int i2 = i > 0 ? i - 1 : 0;
                        int j2 = j > 0 ? j - 1 : 0;
        
                        // Check if the current word contains the keyword
                        if (words[j].Contains(sentence.keys[i].ToLower()))
                        {
                            cell[i, j] = cell[i2, j2] + 1;
                        }
                        else
                        {
                            cell[i, j] = Math.Max(cell[i2, j], cell[i, j2]);
                        }
                    }
                }
        
                // Find the maximum match count in the 2D array
                int maxCell = cell.Cast<int>().Max();
        
                // Update the best match if the current match count is higher
                if (bestMatch[1] < maxCell)
                {
                    bestMatch[0] = tNum;
                    bestMatch[1] = maxCell;
                }
            }
        
            // Return the index of the best match if it meets the overlap threshold, otherwise return -1
            return (bestMatch[1] >= overlapNumber) ? bestMatch[0] : -1;
        }

        // Extract the data from the text based on the specified format and options.
        static string GetData(string text, SentenceAttributes sentenceAttributes, string cultureVariable)
        {
            // Initialize the output variable
            string output = string.Empty;
        
            // Handle splitting logic based on the 'split' attribute
            foreach (var splitItem in sentenceAttributes.split)
            {
                var splitParts = splitItem.Split(new[] { "." }, StringSplitOptions.None);
                var splitPart = splitParts[0];
                var splitDivider = splitParts[1];
        
                if (text.Contains(splitDivider))
                {
                    text = splitPart.Equals("L")
                        ? text.Split(new[] { splitDivider }, StringSplitOptions.None).Last()
                        : text.Split(new[] { splitDivider }, StringSplitOptions.None)[Convert.ToInt32(splitPart)];
                }
            }
        
            // Handle different formats specified in the 'format' attribute
            switch (sentenceAttributes.format.ToLower())
            {
                case "int":
                    // Extract integer values from the text
                    output = Regex.Match(text, @"\d+").Value;
                    break;
        
                case "double":
                    // Extract and format double values
                    output = string.Format(CultureInfo.CreateSpecificCulture(cultureVariable), "{0:0.00}",
                        Convert.ToDouble(string.Join("", text.ToCharArray().Where(char.IsDigit))) / 100);
                    break;
        
                case "string":
                    // Handle string format with options
                    var outputList = new List<string>();
        
                    foreach (var option in sentenceAttributes.options)
                    {
                        var words = text.Trim().ToLower().Split(new[] { " " }, StringSplitOptions.None);
                        var optionKeys = option.Split(new[] { "{", "}" }, StringSplitOptions.None)[1]
                                               .Split(new[] { "," }, StringSplitOptions.None);
        
                        foreach (var key in optionKeys)
                        {
                            if (words.Contains(key.Split('.')[0].ToLower().Trim()))
                            {
                                outputList.Add(option.Replace(option.Substring(option.IndexOf("{"),
                                    option.IndexOf("}") - option.IndexOf("{") + 1), key.Replace(".", "")).Trim());
                                break;
                            }
                        }
                    }
        
                    output = outputList.Any() ? string.Join(", ", outputList) : string.Empty;
                    break;
        
                case "line":
                case "global":
                    // Return the entire text for 'line' or 'global' formats
                    output = text;
                    break;
        
                default:
                    // Default to an empty string if the format is unrecognized
                    output = string.Empty;
                    break;
            }
        
            return output;
        }
        
        // Create a DataTable with columns based on the paragraph and sentence names.
        static DataTable CreateDataTable(ParagraphClass rootObject)
        {
            // Initialize a new DataTable to store the output
            var outputTable = new DataTable();
        
            // Iterate through each paragraph in the root object
            foreach (var targetParagraph in rootObject.paragraph)
            {
                // Iterate through each sentence in the current paragraph
                foreach (var targetSentence in targetParagraph.sentence)
                {
                    // Add a new column to the DataTable for each sentence
                    // The column name is a combination of the paragraph name and sentence name
                    var columnName = $"({targetParagraph.name}) {targetSentence.name}".Trim();
                    outputTable.Columns.Add(columnName, typeof(string));
                }
            }
        
            // Return the constructed DataTable
            return outputTable;
        }

        #endregion


        #region Protected Methods

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            if (Text == null) metadata.AddValidationError(string.Format(Resources.ValidationValue_Error, nameof(Text)));
            if (PathToTheSettingsFile == null) metadata.AddValidationError(string.Format(Resources.ValidationValue_Error, nameof(PathToTheSettingsFile)));
            if (CultureVariable == null) metadata.AddValidationError(string.Format(Resources.ValidationValue_Error, nameof(CultureVariable)));

            base.CacheMetadata(metadata);
        }

        protected override async Task<Action<AsyncCodeActivityContext>> ExecuteAsync(AsyncCodeActivityContext context, CancellationToken cancellationToken)
        {
            // Inputs
            var text = Text.Get(context);
            var pathToTheSettingsFile = PathToTheSettingsFile.Get(context);
            var lineSeparator = LineSeparator.Get(context);
            var cultureVariable = CultureVariable.Get(context);
        
            // Validate inputs
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Input text cannot be null or empty.");
            if (string.IsNullOrWhiteSpace(pathToTheSettingsFile) || !File.Exists(pathToTheSettingsFile))
                throw new FileNotFoundException("The settings file path is invalid or does not exist.");
            if (string.IsNullOrWhiteSpace(lineSeparator))
                throw new ArgumentException("Line separator cannot be null or empty.");
        
            // Read and parse the JSON settings file
            string jsonString = File.ReadAllText(pathToTheSettingsFile);
            var rootObject = JsonSerializer.Deserialize<ParagraphClass>(jsonString);
        
            // Split the input text into paragraphs
            var paragraphsText = GetParagraphs(text, lineSeparator);
        
            // Create the output DataTable
            var outputTable = CreateDataTable(rootObject);
        
            // Initialize a list to store the output row
            var outputRow = new List<string>();
        
            // Define dictionaries for paragraph and sentence processing
            var paragraphsDefined = new Dictionary<string, List<string>>();
        
            // Process each paragraph defined in the JSON settings
            foreach (var targetParagraph in rootObject.paragraph)
            {
                // Find the best matching paragraph in the input text
                var paragraphNumber = ParagraphDefinition(paragraphsText, targetParagraph, 1);
        
                if (paragraphNumber >= 0)
                {
                    // Split the paragraph into sentences
                    if (targetParagraph.sentence.Any(s => s.format.Equals("global", StringComparison.OrdinalIgnoreCase)))
                    {
                        paragraphsDefined[targetParagraph.name] = string.Join(".", paragraphsText)
                            .Split(new[] { "." }, StringSplitOptions.None)
                            .ToList();
                    }
                    else
                    {
                        paragraphsDefined[targetParagraph.name] = paragraphsText[paragraphNumber]
                            .Split(new[] { "." }, StringSplitOptions.None)
                            .ToList();
                    }
        
                    // Process each sentence in the paragraph
                    foreach (var targetSentence in targetParagraph.sentence)
                    {
                        var sentenceNumber = SentenceDefinition(paragraphsDefined[targetParagraph.name], targetSentence, targetSentence.overlap);
        
                        if (sentenceNumber >= 0)
                        {
                            // Extract data from the sentence
                            var data = GetData(paragraphsDefined[targetParagraph.name][sentenceNumber], targetSentence, cultureVariable);
                            outputRow.Add(data);
                        }
                        else
                        {
                            outputRow.Add(string.Empty); // Add an empty value if no match is found
                        }
                    }
                }
                else
                {
                    // Add empty values for all sentences if the paragraph is not found
                    foreach (var targetSentence in targetParagraph.sentence)
                    {
                        outputRow.Add(string.Empty);
                    }
                }
            }
        
            // Add the processed row to the output DataTable
            outputTable.Rows.Add(outputRow.ToArray());
        
            // Outputs
            return (ctx) =>
            {
                Output.Set(ctx, outputTable);
            };
        }

        #endregion
    }
}

