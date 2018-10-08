﻿using System.Runtime.Serialization;
using System.Windows.Forms;

namespace SW2URDF.URDF
{
    //The color element of the material element. Contains a single RGBA.
    [DataContract]
    public class Color : URDFElement
    {
        [DataMember]
        private readonly URDFAttribute RGBAAttribute;

        private double[] RGBA
        {
            get
            {
                return (double[])RGBAAttribute.Value;
            }
            set
            {
                RGBAAttribute.Value = value;
            }
        }

        public double Red
        {
            get
            {
                return RGBA[0];
            }
            set
            {
                RGBA[0] = value;
            }
        }

        public double Green
        {
            get
            {
                return RGBA[1];
            }
            set
            {
                RGBA[1] = value;
            }
        }

        public double Blue
        {
            get
            {
                return RGBA[2];
            }
            set
            {
                RGBA[2] = value;
            }
        }

        public double Alpha
        {
            get
            {
                return RGBA[3];
            }
            set
            {
                RGBA[3] = value;
            }
        }

        public Color() : base("color", false)
        {
            RGBAAttribute = new URDFAttribute("rgba", true, new double[] { 1, 1, 1, 1 });

            Attributes.Add(RGBAAttribute);
        }

        public void FillBoxes(DomainUpDown boxRed, DomainUpDown boxGreen,
            DomainUpDown boxBlue, DomainUpDown boxAlpha, string format)
        {
            string[] rgbaText = RGBAAttribute.GetTextArrayFromDoubleArray(format);
            if (rgbaText != null)
            {
                boxRed.Text = rgbaText[0];
                boxGreen.Text = rgbaText[1];
                boxBlue.Text = rgbaText[2];
                boxAlpha.Text = rgbaText[3];
            }
        }

        public void Update(DomainUpDown boxRed, DomainUpDown boxGreen,
            DomainUpDown boxBlue, DomainUpDown boxAlpha)
        {
            RGBAAttribute.SetDoubleArrayFromStringArray(
                new string[] { boxRed.Text, boxGreen.Text, boxBlue.Text, boxAlpha.Text });
        }

        public void SetColor(double[] rgba)
        {
            Red = rgba[0];
            Green = rgba[1];
            Blue = rgba[2];
            Alpha = rgba[3];
        }

        public double[] GetColor()
        {
            return new double[] { Red, Green, Blue, Alpha };
        }
    }
}
