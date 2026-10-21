using System.Collections.Generic;
using ParcelSort.Core;
using UnityEngine;

namespace ParcelSort.Game.World
{
    /// <summary>Keeps a label or badge turned toward the camera.</summary>
    public class Billboard : MonoBehaviour
    {
        static Camera cam;

        void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }
    }

    /// <summary>
    /// Builds the chunky, toy-like farm props from rounded boxes and cylinders: crops, harvesters,
    /// trees. Every prop shares one material per color. Real models can replace these later
    /// without touching the board logic.
    /// </summary>
    public class FarmKit
    {
        public static readonly Color[] CropColors =
        {
            new Color(0.93f, 0.30f, 0.27f), // tomato
            new Color(0.50f, 0.80f, 0.36f), // cabbage
            new Color(0.34f, 0.46f, 0.92f), // blueberry
            new Color(1.00f, 0.84f, 0.28f), // corn
            new Color(0.60f, 0.38f, 0.80f), // eggplant
            new Color(1.00f, 0.60f, 0.22f), // pumpkin
            new Color(0.97f, 0.95f, 0.87f), // cauliflower
            new Color(0.98f, 0.52f, 0.70f), // radish
        };

        public static readonly Color Leaf = new Color(0.30f, 0.63f, 0.30f);
        public static readonly Color Soil = new Color(0.58f, 0.40f, 0.27f);
        public static readonly Color Bark = new Color(0.55f, 0.38f, 0.25f);
        public static readonly Color Cream = new Color(0.98f, 0.95f, 0.88f);
        public static readonly Color Rubber = new Color(0.27f, 0.27f, 0.30f);
        public static readonly Color Glass = new Color(0.55f, 0.78f, 0.92f);
        public static readonly Color Ink = new Color(0.22f, 0.20f, 0.18f);

        readonly Material baseMaterial;
        readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();

        public FarmKit()
        {
            baseMaterial = Resources.Load<Material>("Materials/Toy");
            if (baseMaterial == null) baseMaterial = new Material(Shader.Find("Standard"));
        }

        public GameObject Prop(string name, Transform parent, Mesh mesh, Color color, Vector3 localPosition)
        {
            return Prop(name, parent, mesh, color, localPosition, Quaternion.identity);
        }

        public GameObject Prop(string name, Transform parent, Mesh mesh, Color color, Vector3 localPosition, Quaternion localRotation)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = MaterialFor(color);
            return go;
        }

        public Material MaterialFor(Color color)
        {
            Material material;
            if (!materials.TryGetValue(color, out material))
            {
                material = new Material(baseMaterial) { color = color };
                materials[color] = material;
            }
            return material;
        }

        static Mesh Ball(float x, float y, float z)
        {
            return MeshFactory.RoundedBox(new Vector3(x, y, z), Mathf.Min(x, Mathf.Min(y, z)) / 2, 6);
        }

        static Mesh Ball(float d)
        {
            return Ball(d, d, d);
        }

        public GameObject Box(string name, Transform parent, Vector3 size, float radius, Color color, Vector3 position)
        {
            return Prop(name, parent, MeshFactory.RoundedBox(size, radius, 3), color, position);
        }

        // ---- crops ---------------------------------------------------------------------------

        /// <summary>A crop sitting on the ground at the root's origin, about 0.7 units across.</summary>
        public GameObject Crop(int crop, Transform parent, Vector3 position)
        {
            var root = new GameObject(Crops.Names[crop]).transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            Color c = CropColors[crop];
            switch (crop)
            {
                case 0: // tomato
                    Prop("Fruit", root, Ball(0.62f, 0.56f, 0.62f), c, new Vector3(0, 0.30f, 0));
                    Prop("Calyx", root, Ball(0.30f, 0.08f, 0.30f), Leaf, new Vector3(0, 0.58f, 0));
                    break;
                case 1: // cabbage
                    Prop("Head", root, Ball(0.74f, 0.58f, 0.74f), c, new Vector3(0, 0.29f, 0));
                    Prop("Heart", root, Ball(0.42f), Color.Lerp(c, Color.white, 0.35f), new Vector3(0, 0.42f, 0));
                    break;
                case 2: // blueberries
                    Prop("Bush", root, Ball(0.62f, 0.32f, 0.62f), Leaf, new Vector3(0, 0.16f, 0));
                    Prop("Berry", root, Ball(0.30f), c, new Vector3(-0.14f, 0.38f, -0.08f));
                    Prop("Berry", root, Ball(0.30f), c, new Vector3(0.15f, 0.38f, -0.06f));
                    Prop("Berry", root, Ball(0.30f), c, new Vector3(0.0f, 0.42f, 0.14f));
                    break;
                case 3: // corn
                    Prop("Leaf", root, MeshFactory.RoundedBox(new Vector3(0.12f, 0.55f, 0.06f), 0.03f, 2), Leaf,
                        new Vector3(-0.17f, 0.28f, 0), Quaternion.Euler(0, 0, 25));
                    Prop("Leaf", root, MeshFactory.RoundedBox(new Vector3(0.12f, 0.55f, 0.06f), 0.03f, 2), Leaf,
                        new Vector3(0.17f, 0.28f, 0), Quaternion.Euler(0, 0, -25));
                    Prop("Cob", root, Ball(0.32f, 0.78f, 0.32f), c, new Vector3(0, 0.40f, 0));
                    break;
                case 4: // eggplant
                    Prop("Fruit", root, Ball(0.38f, 0.66f, 0.38f), c, new Vector3(0, 0.30f, 0), Quaternion.Euler(0, 0, 28));
                    Prop("Cap", root, Ball(0.22f, 0.12f, 0.22f), Leaf, new Vector3(-0.15f, 0.58f, 0), Quaternion.Euler(0, 0, 28));
                    break;
                case 5: // pumpkin
                    Prop("Fruit", root, Ball(0.76f, 0.50f, 0.70f), c, new Vector3(0, 0.25f, 0));
                    Prop("Stem", root, MeshFactory.RoundedBox(new Vector3(0.08f, 0.18f, 0.08f), 0.03f, 2), Bark, new Vector3(0, 0.55f, 0));
                    break;
                case 6: // cauliflower
                    for (int i = 0; i < 4; i++)
                    {
                        Prop("Leaf", root, Ball(0.34f, 0.10f, 0.20f), Leaf, Quaternion.Euler(0, 45 + i * 90, 0) * new Vector3(0.24f, 0.08f, 0),
                            Quaternion.Euler(0, 45 + i * 90, 0));
                    }
                    Prop("Curd", root, Ball(0.56f, 0.48f, 0.56f), c, new Vector3(0, 0.30f, 0));
                    break;
                default: // radish
                    Prop("Root", root, Ball(0.48f), c, new Vector3(0, 0.24f, 0));
                    Prop("Leaf", root, Ball(0.14f, 0.40f, 0.08f), Leaf, new Vector3(-0.07f, 0.62f, 0), Quaternion.Euler(0, 0, 18));
                    Prop("Leaf", root, Ball(0.14f, 0.40f, 0.08f), Leaf, new Vector3(0.07f, 0.62f, 0), Quaternion.Euler(0, 0, -18));
                    break;
            }
            return root.gameObject;
        }

        // ---- harvester -----------------------------------------------------------------------

        /// <summary>
        /// A chunky toy combine harvester facing +Z, about 1.2 long. The capacity badge floats above it;
        /// its TextMesh is returned so the board can count down as crops come in.
        /// </summary>
        public GameObject Harvester(int crop, int capacity, Transform parent, Vector3 position, out TextMesh label)
        {
            var root = new GameObject("Harvester").transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            Color body = CropColors[crop];
            // Cauliflower white needs a little more contrast against the cream cab.
            if (crop == 6) body = new Color(0.90f, 0.88f, 0.80f);
            Color dark = Color.Lerp(body, Color.black, 0.25f);
            var sideways = Quaternion.Euler(0, 90, 0);

            Prop("Wheel", root, MeshFactory.Cylinder(0.27f, 0.18f), Rubber, new Vector3(-0.42f, 0.27f, 0.12f), sideways);
            Prop("Wheel", root, MeshFactory.Cylinder(0.27f, 0.18f), Rubber, new Vector3(0.42f, 0.27f, 0.12f), sideways);
            Prop("Wheel", root, MeshFactory.Cylinder(0.18f, 0.16f), Rubber, new Vector3(-0.36f, 0.18f, -0.42f), sideways);
            Prop("Wheel", root, MeshFactory.Cylinder(0.18f, 0.16f), Rubber, new Vector3(0.36f, 0.18f, -0.42f), sideways);

            Prop("Body", root, MeshFactory.RoundedBox(new Vector3(0.72f, 0.48f, 0.92f), 0.16f, 4), body, new Vector3(0, 0.50f, -0.10f));
            Prop("Hopper", root, MeshFactory.RoundedBox(new Vector3(0.56f, 0.22f, 0.36f), 0.08f, 3), dark, new Vector3(0, 0.82f, -0.34f));
            Prop("Cab", root, MeshFactory.RoundedBox(new Vector3(0.50f, 0.44f, 0.40f), 0.14f, 4), Cream, new Vector3(0, 0.92f, 0.06f));
            Prop("Window", root, MeshFactory.RoundedBox(new Vector3(0.40f, 0.24f, 0.04f), 0.02f, 1), Glass, new Vector3(0, 0.96f, 0.27f));
            Prop("Header", root, MeshFactory.RoundedBox(new Vector3(1.06f, 0.26f, 0.30f), 0.10f, 3), dark, new Vector3(0, 0.30f, 0.56f));
            Prop("Reel", root, MeshFactory.Cylinder(0.12f, 0.98f), Cream, new Vector3(0, 0.40f, 0.62f), sideways);

            var badge = new GameObject("Badge", typeof(Billboard)).transform;
            badge.SetParent(root, false);
            badge.localPosition = new Vector3(0, 1.55f, 0);
            Prop("Disc", badge, MeshFactory.Cylinder(0.30f, 0.06f), Color.white, Vector3.zero);
            Prop("Ring", badge, MeshFactory.Cylinder(0.34f, 0.04f), body, new Vector3(0, 0, 0.02f));

            var text = new GameObject("Count", typeof(MeshRenderer), typeof(TextMesh));
            text.transform.SetParent(badge, false);
            text.transform.localPosition = new Vector3(0, 0, -0.05f);
            label = text.GetComponent<TextMesh>();
            label.font = Ui.Font;
            label.fontSize = 64;
            label.characterSize = 0.07f;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontStyle = FontStyle.Bold;
            label.color = Ink;
            label.text = capacity.ToString();
            text.GetComponent<MeshRenderer>().sharedMaterial = Ui.Font.material;
            return root.gameObject;
        }

        // ---- scenery -------------------------------------------------------------------------

        public GameObject Tree(Transform parent, Vector3 position, float scale)
        {
            var root = new GameObject("Tree").transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            root.localScale = Vector3.one * scale;
            Prop("Trunk", root, MeshFactory.Cylinder(0.16f, 0.8f), Bark, new Vector3(0, 0.4f, 0), Quaternion.Euler(-90, 0, 0));
            Prop("Canopy", root, Ball(1.1f, 0.95f, 1.1f), new Color(0.36f, 0.70f, 0.36f), new Vector3(0, 1.1f, 0));
            Prop("Canopy", root, Ball(0.7f), new Color(0.44f, 0.78f, 0.40f), new Vector3(0.15f, 1.55f, -0.1f));
            return root.gameObject;
        }

        /// <summary>A red toy barn, about 4 wide and 3 tall, doors facing -Z (toward the field).</summary>
        public GameObject Barn(Transform parent, Vector3 position)
        {
            var root = new GameObject("Barn").transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            var red = new Color(0.88f, 0.36f, 0.32f);
            var roof = new Color(0.45f, 0.40f, 0.42f);
            Prop("Walls", root, MeshFactory.RoundedBox(new Vector3(3.6f, 1.8f, 2.4f), 0.15f, 3), red, new Vector3(0, 0.9f, 0));
            Prop("Roof", root, MeshFactory.RoundedBox(new Vector3(2.3f, 0.22f, 2.7f), 0.08f, 2), roof,
                new Vector3(-0.9f, 2.25f, 0), Quaternion.Euler(0, 0, 32));
            Prop("Roof", root, MeshFactory.RoundedBox(new Vector3(2.3f, 0.22f, 2.7f), 0.08f, 2), roof,
                new Vector3(0.9f, 2.25f, 0), Quaternion.Euler(0, 0, -32));
            Prop("Gable", root, MeshFactory.RoundedBox(new Vector3(1.9f, 1.0f, 2.2f), 0.1f, 2), red,
                new Vector3(0, 1.95f, 0), Quaternion.Euler(0, 0, 45));
            Prop("Door", root, MeshFactory.RoundedBox(new Vector3(1.3f, 1.3f, 0.08f), 0.05f, 2), Cream, new Vector3(0, 0.7f, -1.22f));
            Prop("Door", root, MeshFactory.RoundedBox(new Vector3(1.1f, 1.1f, 0.06f), 0.04f, 2), red, new Vector3(0, 0.65f, -1.26f));
            Prop("Brace", root, MeshFactory.RoundedBox(new Vector3(1.4f, 0.1f, 0.05f), 0.02f, 1), Cream,
                new Vector3(0, 0.65f, -1.3f), Quaternion.Euler(0, 0, 45));
            Prop("Brace", root, MeshFactory.RoundedBox(new Vector3(1.4f, 0.1f, 0.05f), 0.02f, 1), Cream,
                new Vector3(0, 0.65f, -1.3f), Quaternion.Euler(0, 0, -45));
            Prop("Silo", root, MeshFactory.Cylinder(0.55f, 3.0f), new Color(0.80f, 0.84f, 0.88f), new Vector3(2.4f, 1.5f, 0.3f),
                Quaternion.Euler(-90, 0, 0));
            Prop("Silo cap", root, MeshFactory.RoundedBox(new Vector3(1.1f, 0.6f, 1.1f), 0.3f, 4), red, new Vector3(2.4f, 3.0f, 0.3f));
            return root.gameObject;
        }

        public GameObject Bush(Transform parent, Vector3 position)
        {
            return Prop("Bush", parent, Ball(0.7f, 0.45f, 0.6f), new Color(0.40f, 0.72f, 0.38f), position + new Vector3(0, 0.2f, 0));
        }
    }
}
