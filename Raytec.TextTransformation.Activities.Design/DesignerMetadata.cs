using System.Activities.Presentation.Metadata;
using System.ComponentModel;
using System.ComponentModel.Design;
using Raytec.TextTransformation.Activities.Design.Designers;
using Raytec.TextTransformation.Activities.Design.Properties;

namespace Raytec.TextTransformation.Activities.Design
{
    public class DesignerMetadata : IRegisterMetadata
    {
        public void Register()
        {
            var builder = new AttributeTableBuilder();
            builder.ValidateTable();

            var categoryAttribute = new CategoryAttribute($"{Resources.Category}");

            builder.AddCustomAttributes(typeof(ContractDataRecognition), categoryAttribute);
            builder.AddCustomAttributes(typeof(ContractDataRecognition), new DesignerAttribute(typeof(ContractDataRecognitionDesigner)));
            builder.AddCustomAttributes(typeof(ContractDataRecognition), new HelpKeywordAttribute(""));


            MetadataStore.AddAttributeTable(builder.CreateTable());
        }
    }
}
