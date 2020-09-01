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
        // �������� ������ �������
        static List<string> GetParagraphs(string inputText, string lineSeparator)
        {
            // ������ ������� ��� ������� ������� ���������� � ����� ����� ����
            var splitText = inputText.Split(new[] { lineSeparator, Environment.NewLine, "\r\n", "\n" }, StringSplitOptions.None).ToList();
            var paragraphsText = new List<string>();
            var tempText = new List<string>();

            tempText.Add(splitText[0]);
            for (int i = 1; i < splitText.Count(); i++)
            {
                if (splitText[i].Count() > 1)
                {
                    if (Char.IsDigit(splitText[i][0]) && splitText[i][1].Equals('.') && tempText.Count() > 0 || splitText[i][0].Equals('-'))
                    {
                        paragraphsText.Add(String.Join(" ", tempText));
                        tempText = new List<string>();
                        tempText.Add(splitText[i]);
                    }
                    else
                    {
                        tempText.Add(splitText[i]);
                    }

                }

                if (i == splitText.Count() - 1 && tempText.Count() > 0)
                {
                    paragraphsText.Add(String.Join(" ", tempText));
                }

            }


            // ������� ����� �� ���������
            var paragraphSubstring = new List<string>();

            foreach (var item in paragraphsText)
            {
                var charCounter = 0;

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

                paragraphSubstring.Add(item.Substring(charCounter).Trim());

            }

            return paragraphSubstring;

        }

        // ���������� �������������� �������
        static int ParagraphDefinition(List<string> text, ParagraphAttributes paragraph, int overlapNumber = 1)
        {
            var bestMatch = new int[2];

            for (int tNum = 0; tNum < text.Count(); tNum++)
            {
                var words = text[tNum].ToLower().Split(new[] { " " }, StringSplitOptions.None);
                var cell = new int[paragraph.keys.Count(), words.Count()];

                // ������� ����� � �������
                for (int i = 0; i < paragraph.keys.Count(); i++)
                {


                    for (int j = 0; j < words.Count(); j++)
                    {
                        int i2 = i < 1 ? 0 : i - 1;
                        int j2 = j < 1 ? 0 : j - 1;

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

                int maxCell = cell.Cast<int>().Max();

                if (bestMatch[1] < maxCell)
                {
                    bestMatch[0] = tNum;
                    bestMatch[1] = maxCell;
                }

            }

            return ((bestMatch[0] > 0 || bestMatch[1] > 0) && bestMatch[1] >= overlapNumber ? bestMatch[0] : -1);
        }

        // ���������� �������������� ����������� 
        static int SentenceDefinition(List<string> text, SentenceAttributes sentence, int overlapNumber = 1)
        {
            var bestMatch = new int[2];

            for (int tNum = 0; tNum < text.Count(); tNum++)
            {
                var words = text[tNum].ToLower().Split(new[] { " " }, StringSplitOptions.None);
                var cell = new int[sentence.keys.Count(), words.Count()];

                // ������� ����� � �������
                for (int i = 0; i < sentence.keys.Count(); i++)
                {


                    for (int j = 0; j < words.Count(); j++)
                    {
                        int i2 = i < 1 ? 0 : i - 1;
                        int j2 = j < 1 ? 0 : j - 1;

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

                int maxCell = cell.Cast<int>().Max();

                if (bestMatch[1] < maxCell)
                {
                    bestMatch[0] = tNum;
                    bestMatch[1] = maxCell;
                }

            }

            return ((bestMatch[0] > 0 || bestMatch[1] > 0) && bestMatch[1] >= overlapNumber ? bestMatch[0] : -1);
        }

        // �������� ������ � �����������
        static string GetData(string text, SentenceAttributes sentenceAttributes, string cultureVariable)
        {
            /*
            ������ ����� ����� ��������� ���������:
            - �������� (int)
            - ����� � ��������� ������, ��� ����� ����� ������� (double)
            - ���������
            -- � �������� ���������������� ���������
            -- ��� ���������
            */

            string output = String.Empty;
            var temp = new List<string>();

            foreach (var splitItem in sentenceAttributes.split)
            {
                var splitPart = splitItem.Split(new[] { "." }, StringSplitOptions.None)[0];
                var splitDivider = splitItem.Split(new[] { "." }, StringSplitOptions.None)[1];

                if (text.Contains(splitDivider))
                {
                    if (splitPart.Equals("L"))
                    {
                        text = text.Split(new[] { splitDivider }, StringSplitOptions.None).Last();
                    }
                    else
                    {
                        text = text.Split(new[] { splitDivider }, StringSplitOptions.None)[Convert.ToInt32(splitPart)];
                    }
                }
            }

            // ������� �������� � ������

            if (sentenceAttributes.format.Equals("int")) // ���� ������ �������� (int) - ������������ ������ ������ ��������� ����� � ������ ����� ������� (split)
            {
                output = (Regex.Match(text, @"\d+").Value).ToString();

            }
            else if (sentenceAttributes.format.Equals("double")) // ���� ������ ����� � ��������� ������ (double) - ������������ ��� ����� ��������� � ������ ����� ������� (split)
            {
                output = String.Format(CultureInfo.CreateSpecificCulture("ru-RU"), "{0:0.00}",
                Convert.ToDouble(string.Join("", text.ToCharArray().Where(Char.IsDigit))) / 100);
            }
            else if (sentenceAttributes.format.Equals("string"))
            {

                var outputList = new List<string>();

                // ����������� ��� ����� � sentenceAttributes 
                for (int oNum = 0; oNum < sentenceAttributes.options.Count(); oNum++)
                {
                    
                    // ������ ����� �� �����
                    var words = text.Trim().ToLower().Split(new[] { " " }, StringSplitOptions.None);

                    // � ������ ����� �������� �����, ������� �������� ������ ������ "{ }" ��������� ������� ����� ����� ���� ������ ����
                    var optionKeys = sentenceAttributes.options[oNum].Split(new[] { "{", "}" }, StringSplitOptions.None)[1].Split(new[] { "," }, StringSplitOptions.None);

                    // ������� ����� � �������
                    for (int i = 0; i < optionKeys.Count(); i++)
                    {
                        if (text.Trim().ToLower().Contains(optionKeys[i].Split(new[] { "." }, StringSplitOptions.None)[0].ToLower().Trim()))
                        {
                            outputList.Add(sentenceAttributes.options[oNum].Replace(sentenceAttributes.options[oNum].Substring(sentenceAttributes.options[oNum].IndexOf("{"), (sentenceAttributes.options[oNum].IndexOf("}") - sentenceAttributes.options[oNum].IndexOf("{") + 1)), optionKeys[i].Replace(".", "")).Trim());
                            break;
                        }
                    }
                }

                // �������� ������ ������ ����� �������� ������ � UiPath
                if (output.Equals(null))
                {
                    output = "";
                }
                else
                {
                    output = String.Join(", ", outputList);
                }

            }
            else if (sentenceAttributes.format.Equals("line") || sentenceAttributes.format.Equals("global"))
            {
                output = text;
            }
            else
            {
                output = "";
            }

            return output;

        }

        // ���������� �������� �������
        static DataTable CreateDataTable(ParagraphClass rootObject)
        {
            var outputTable = new DataTable();

            foreach (var targetParagraph in rootObject.paragraph)
            {
                foreach (var targetSentence in targetParagraph.sentence)
                {
                    // ��������� ������� � �������� �������
                    outputTable.Columns.Add(("(" + targetParagraph.name + ") " + targetSentence.name).Trim(), typeof(System.String));

                }
            }

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
            var pathToTheSettingsFile = @PathToTheSettingsFile.Get(context);
            var lineSeparator = LineSeparator.Get(context);
            var cultureVariable = CultureVariable.Get(context);

            ///////////////////////////
            // �������� ������������ ������ �� UiPath
            var inputText = text;

            // ��������� ��������� �� ����������� ������
            var paragraphsText = GetParagraphs(inputText, lineSeparator);

            // �������� �������� ����� �� ����� (����� UiPath)
            string jsonFilePath = pathToTheSettingsFile; // UiPath ��������� ���� � �����
            string jsonString = File.ReadAllText(jsonFilePath);
            var rootObject = JsonSerializer.Deserialize<ParagraphClass>(jsonString);

            /*
            ������ ������ JSON. ������������ ���������� �����,
            �� � UiPath
            */

            /*
            ���������:
            - inputText
            - paragraphsText
            - paragraphsDefined
            */

            // ������� �������� �������
            var outputTable = CreateDataTable(rootObject);

            // ������� ��������� ������
            var outputRow = new List<string>();
            // ----------------- //

            // ���������� ������ � ������
            //var paragraphsText = GetParagraphs(inputText); // ������ ���������� �� ��������� ������

            // ���������� �������������� ��� ���������� ��� ������ � �������
            var paragraphsDefined = new Dictionary<string, List<string>>(); // ���������� ��� ����������� ����������
            var sentenceDefined = new Dictionary<string, string>(); // ���������� ��� ����������� �����������

            // �� �������� ������ ���������� �������������� ������ (� ��������� ����� ������� ������������� �� ���������)      

            // ��� ������� �������� ��������� �� ��������, ���������� ������������, ���������� ������������ �����������
            // ����� �������� ������ �� ��������
            foreach (var targetParagraph in rootObject.paragraph) // ����������� ��� ���������, ��������� � ���������� (keywords.json)
            {
                var paragraphNumber = ParagraphDefinition(paragraphsText, targetParagraph, 1); // ���� �������� � ������ �� ��������� �������� ����

                if (paragraphNumber >= 0)
                {
                    

                    foreach (var targetSentence in targetParagraph.sentence) // ����������� ��� �����������, ��������� � ���������� (keywords.json)
                    {

                        if (targetSentence.format.Equals("global"))
                        {
                            paragraphsDefined[targetParagraph.name] = String.Join(".", paragraphsText).Split(new[] { "." }, StringSplitOptions.None).ToList();
                        }
                        else
                        {
                            paragraphsDefined[targetParagraph.name] = paragraphsText[paragraphNumber].Split(new[] { "." }, StringSplitOptions.None).ToList();
                        }

                        var sentenceNumber = SentenceDefinition(paragraphsDefined[targetParagraph.name], targetSentence, targetSentence.overlap);

                        if (sentenceNumber >= 0)
                        {
                            var data = GetData(paragraphsDefined[targetParagraph.name][sentenceNumber], targetSentence, cultureVariable); // ���������� ����������� ������
                            outputRow.Add(data);
                        }
                        else
                        {
                            outputRow.Add("");
                        }

                    }
                }
                else
                {
                    foreach (var targetSentence in targetParagraph.sentence) // ���� �������� �� ���������, �� ���� ������� ��������� ��������� �������� ��������
                    {
                        outputRow.Add("");
                    }
                }
            }

            outputTable.Rows.Add(outputRow.ToArray());
            ///////////////////////////

            // Outputs
            return (ctx) => {
                Output.Set(ctx, outputTable);
            };
        }

        #endregion
    }
}

