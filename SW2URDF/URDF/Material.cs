using System.Runtime.Serialization;
using System.Windows.Forms;

namespace SW2URDF.URDF
{
    //The material element of the visual element.
    [DataContract(IsReference = true, Namespace = "http://schemas.datacontract.org/2004/07/SW2URDF")]
    public class Material : URDFElement
    {
        [DataMember]
        public readonly Color Color;

        [DataMember]
        public readonly Texture Texture;

        [DataMember]
        public readonly URDFAttribute NameAttribute;

        public string Name
        {
            get
            {
                return (string)NameAttribute.Value;
            }
            set
            {
                NameAttribute.Value = value;
            }
        }

        public Material() : base("material", false)
        {
            Color = new Color();
            Texture = new Texture();
            NameAttribute = new URDFAttribute("name", true, "");

            Attributes.Add(NameAttribute);
            ChildElements.Add(Color);
            ChildElements.Add(Texture);
        }

        public void FillBoxes(ComboBox box, string format)
        {
            box.Text = Name;
        }

        /// <summary>
        /// Material is not required, but name is if the material is specified. We must populate it
        /// with something
        /// </summary>
        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            SetRequired(false);
            NameAttribute.SetRequired(true);
            if (NameAttribute.Value == null)
            {
                NameAttribute.Value = "";
            }
        }
    }
}