﻿using VrmLib;

namespace Vrm10
{
    public static class ModelExtensions
    {
        public static byte[] ToGlb(this VrmLib.Model model)
        {
            // export vrm-1.0
            var exporter10 = new Vrm10.Vrm10Exporter();
            var option = new VrmLib.ExportArgs
            {
                // vrm = false
            };
            var glbBytes10 = exporter10.Export(model, option);
            var glb10 = VrmLib.Glb.Parse(glbBytes10);
            return glb10.ToBytes();
        }
    }
}
