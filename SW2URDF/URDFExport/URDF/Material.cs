﻿using System.Runtime.Serialization;
using System.Windows.Forms;

namespace SW2URDF.URDFExport.URDF
{
    //The material element of the visual element.
    [DataContract(IsReference = true)]
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
            NameAttribute = new URDFAttribute("name", false, null);

            Attributes.Add(NameAttribute);
            ChildElements.Add(Color);
            ChildElements.Add(Texture);
        }

        public void FillBoxes(ComboBox box, string format)
        {
            box.Text = Name;
        }

        /// <summary>
        /// The name was previously required, but it's not actually. We need to correct
        /// previous serializations.
        /// </summary>
        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            SetRequired(false);
            NameAttribute.SetRequired(false);
        }
    }
}