// Copyright 2006 Herre Kuijpers - <herre@xs4all.nl>
//
// This source file(s) may be redistributed, altered and customized
// by any means PROVIDING the authors name and all copyright
// notices remain intact.
// THIS SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED. USE IT AT YOUR OWN RISK. THE AUTHOR ACCEPTS NO
// LIABILITY FOR ANY DATA DAMAGE/LOSS THAT THIS PRODUCT MAY CAUSE.
//-----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Text;

namespace RayTracer
{
    public class SolidMaterial : BaseMaterial
    {
        private Color color;
        public SolidMaterial(Color color, double reflection, double transparency, double gloss)
        {
            this.color = color;
            this.Reflection = reflection;
            this.Transparency = transparency;
            this.Gloss = gloss;

        }

        public override bool HasTexture { 
            get { return false; } 
        }

        public override Color GetColor(double u, double v)
        {
            return color;
        }
    }
}
