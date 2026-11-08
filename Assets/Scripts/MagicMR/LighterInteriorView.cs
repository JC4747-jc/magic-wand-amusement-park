using UnityEngine;
namespace MagicMR
{
    // Reference-based piezoelectric cutaway, fitted to the existing lighter shell.
    internal sealed class LighterInteriorView
    {
        readonly GameObject root;
        public bool Visible => root != null && root.activeSelf;
        public LighterInteriorView(Transform owner, Renderer[] body)
        {
            var prefab = Resources.Load<GameObject>("MagicMR/LighterInterior");
            if (prefab == null) throw new System.InvalidOperationException("LighterInterior prefab is missing. Run LighterInteriorBuilder.Build.");
            root = Object.Instantiate(prefab, owner, false);
            root.name = "LighterInterior_Piezoelectric";
            var target = BoundsIn(owner, body);
            var source = BoundsIn(root.transform, root.transform.Find("TransparentShell").GetComponentsInChildren<Renderer>());
            var scale = new Vector3(target.size.x/source.size.x,target.size.y/source.size.y,target.size.z/source.size.z);
            root.transform.localScale = scale;
            root.transform.localPosition = target.center-Vector3.Scale(source.center,scale);
            root.SetActive(false);
        }
        static Bounds BoundsIn(Transform space, Renderer[] renderers)
        {
            var bounds = new Bounds(); bool found = false;
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                var b = filter.sharedMesh.bounds;
                for (int i=0;i<8;i++)
                {
                    var sign = new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1);
                    var p = space.InverseTransformPoint(renderer.transform.TransformPoint(b.center+Vector3.Scale(b.extents,sign)));
                    if (!found) {bounds=new Bounds(p,Vector3.zero);found=true;} else bounds.Encapsulate(p);
                }
            }
            return bounds;
        }
        public void Show() {root.SetActive(true);}
        public void Hide() {if(root!=null)root.SetActive(false);}
        public void Dispose() {if(root!=null)Object.Destroy(root);}
    }
}
