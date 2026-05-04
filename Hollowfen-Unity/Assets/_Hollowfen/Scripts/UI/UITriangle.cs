using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    public class UITriangle : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            var v = UIVertex.simpleVert;
            v.color = color;

            v.position = new Vector3(r.xMin, r.yMin, 0f);
            vh.AddVert(v);
            v.position = new Vector3(r.xMax, r.yMin, 0f);
            vh.AddVert(v);
            v.position = new Vector3(r.center.x, r.yMax, 0f);
            vh.AddVert(v);

            vh.AddTriangle(0, 1, 2);
        }
    }
}
