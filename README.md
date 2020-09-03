# Custom UiPath Action: Raytec.TextTransformation

This repository contains a custom UiPath activity designed to perform advanced text transformation and contract data recognition. The activity is built using .NET and integrates seamlessly with UiPath Studio.

## Features

- **Contract Data Recognition**: Extracts structured data from unstructured text using customizable JSON-based settings.
- **Localization Support**: Includes support for multiple languages and cultures.
- **Customizable Input and Output**: Accepts various input formats and outputs results in a DataTable.
- **Error Handling**: Includes options to continue execution even if the activity encounters errors.

## Installation

1. Clone the repository:

   ```bash
   git clone https://github.com/your-repo/Raytec.TextTransformation.git

2. Open the solution file Raytec.TextTransformation.sln in Visual Studio.

3. Build the solution to generate the .nupkg file.

4. Import the .nupkg file into UiPath Studio via the Manage Packages window.

## Usage

1. Drag and drop the ContractDataRecognition activity into your UiPath workflow.

2. Configure the following properties:

- Text: Input text to be processed.
- PathToTheSettingsFile: Path to the JSON file containing recognition settings.
- LineSeparator: Character or string used to separate lines in the input text.
- CultureVariable: Culture information (e.g., en-US, ru-RU).
- Output: DataTable to store the extracted data.

3. Run the workflow to process the input text and extract structured data.

## JSON Settings File

The activity uses a JSON file to define the structure and rules for data extraction.

```json
{
  "paragraph": [
    {
      "name": "Paragraph1",
      "keys": ["key1", "key2"],
      "sentence": [
        {
          "name": "Sentence1",
          "keys": ["keyA", "keyB"],
          "format": "string",
          "overlap": 1,
          "options": ["{option1}", "{option2}"],
          "split": ["L.separator"]
        }
      ]
    }
  ]
}
```

## Development

### Prerequisites

- Visual Studio 2019 or later
- .NET Framework 4.6.1
- UiPath Studio

### Building the Project

1. Open the solution file Raytec.TextTransformation.sln in Visual Studio.
2. Restore NuGet packages.
3. Build the solution.

### Testing

Unit tests can be added to validate the functionality of the activity. Use the Output property to verify the extracted data.

## Contributing

Contributions are welcome! Please fork the repository and submit a pull request with your changes.

## License

This project is licensed under the Apache-2.0 License. See the LICENSE file for details.

## Support

For any issues or questions, please open an issue in the repository or contact the maintainer.
