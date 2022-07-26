using UnityEngine;
using System;
using System.Collections.Generic;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;
namespace RosSharp.RosBridgeClient
{
    public delegate void OnNewPointCloud2(Vector3[] points, Color[] colors);

    [RequireComponent(typeof(RosConnector))]
    public class PCLSubscriber : UnitySubscriber<PointCloud2>//, IMovePlayer
    {
        public enum ChangeColorbyHight { None, Ful, Mono }

        public ChangeColorbyHight colorbyHight = ChangeColorbyHight.Ful;

        public Color PointColor = new Color32(181, 250, 20, 255);
        private Color[] colors;

        public float low_line = 0.9f;
        public float hi_line = 1.4f;
        public Color lowcolor = new Color32(233, 135, 249, 255);
        public Color midcolor = new Color32(164, 153, 188, 255);
        public Color hicolor = new Color32(67, 186, 195, 255);

        public float PointSize = 0.03f;

        private Mesh mesh;
        private Material material;
        private MaterialPropertyBlock materialPropertyBlock;

        public bool Enabled = false;
        private bool previousEnabled = false;
        private string subscribingId;

        // PointCloud2形式の変換に利用
        private byte[] byteArray;
        private int size;
        private uint point_step;


        // 最新のPointCloudデータ（Unity座標）
        public Vector3[] pointCloud { get; protected set; }
        private Color[] rgbColors;
        public bool IsSnapShotUpdated { get; protected set; }
        bool isMessageReceived;

        protected override void Start()
        {
            IsSnapShotUpdated = false;
            base.Start();
            this.pointCloud = new Vector3[0];

            materialPropertyBlock = new MaterialPropertyBlock();

            CreateCubeMesh();
        }

        private void Update()
        {
            if (isMessageReceived)
            {
                ProcessMessage();
                IsSnapShotUpdated = true;
            }
            else
            {
                IsSnapShotUpdated = false;
            }
            DrawByInstancing();
        }

        // 各メッセージタイプ固有のセットアップをBefore～に記述する．
        void BeforeSubscribe()
        {

        }
        void BeforeUnsubscribe()
        {

        }

        protected override void ReceiveMessage(PointCloud2 message)
        {
            uint x_offset = 0;
            uint y_offset = 4;
            uint z_offset = 8;
            uint color_offset = 16;

            bool colorEnabled = false;

            for (int j = 0; j < message.fields.Length; j++)
            {
                PointField field = message.fields[j];
                if (field.name == "x")
                {
                    x_offset = field.offset;
                }
                else if (field.name == "y")
                {
                    y_offset = field.offset;
                }
                else if (field.name == "z")
                {
                    z_offset = field.offset;
                }
                else if (field.name == "rgb")
                {
                    color_offset = field.offset;
                    colorEnabled = true;
                }
            }

            size = message.data.GetLength(0);
            int i = 0;

            byteArray = new byte[size];
            foreach (byte temp in message.data)
            {
                byteArray[i] = temp;  //byte型を取得
                i++;
            }
            point_step = message.point_step;
            size = size / (int)point_step;

            // PointCloud2生データをUnity座標系の3次元点群に変換
            Vector3[] pcl = new Vector3[size];
            Color[] colors = new Color[size];

            for (int n = 0; n < size; n++)
            {
                // byte型をfloatに変換         
                int x_posi = n * (int)point_step + (int)x_offset;
                int y_posi = n * (int)point_step + (int)y_offset;
                int z_posi = n * (int)point_step + (int)z_offset;
                int color_posi = n * (int)point_step + (int)color_offset;

                float x = BitConverter.ToSingle(byteArray, x_posi);
                float y = BitConverter.ToSingle(byteArray, y_posi);
                float z = BitConverter.ToSingle(byteArray, z_posi);

                Vector3 RosPosition = new Vector3(x, y, z);
                Vector3 UnityPosition = CoordinateConvert.RosToUnity(RosPosition);
                pcl[n] = UnityPosition;

                // 
                if (colorEnabled)
                {
                    byte b = byteArray[color_posi];
                    byte g = byteArray[color_posi + 1];
                    byte r = byteArray[color_posi + 2];
                    colors[n] = new Color(r / 255f, g / 255f, b / 255f);
                }
            }
            this.pointCloud = pcl;
            this.rgbColors = colorEnabled ? colors : null;

            isMessageReceived = true;
        }

        private void ProcessMessage()
        {
            isMessageReceived = false;
        }

        void DrawByInstancing()
        {
            if (this.pointCloud == null || pointCloud.Length == 0)
            {
                return;
            }

            /*
            1回のGPU Instancingで直接レンダリングできるオブジェクトが1023個
            */

            int _num_of_objs_per_single_draw = 1023;
            int _num_of_draw = (pointCloud.Length / _num_of_objs_per_single_draw) + 1;

            // Debug.Log($"Num of Points : {pointCloud.Length}");
            // Debug.Log($"Num of Draw: {_num_of_draw}");

            for (int draw_count = 0; draw_count < _num_of_draw; draw_count++)
            {
                int _draw_start_idx = draw_count * _num_of_objs_per_single_draw;
                int _draw_end_idx = _draw_start_idx + _num_of_objs_per_single_draw - 1;
                if (_draw_end_idx >= pointCloud.Length)
                {
                    _draw_end_idx = pointCloud.Length - 1;
                }
                int _point_num = _draw_end_idx - _draw_start_idx + 1;

                // Point Position 
                Matrix4x4[] pointMats = new Matrix4x4[_point_num];
                for (int i = _draw_start_idx; i < _draw_end_idx; i++)
                {
                    Matrix4x4 mat = Matrix4x4.TRS(transform.TransformPoint(pointCloud[i]), Quaternion.identity, Vector3.one * PointSize);
                    pointMats[i - _draw_start_idx] = mat;
                }

                // Point Color
                Vector4[] cs = new Vector4[_point_num];
                for (int i = _draw_start_idx; i < _draw_end_idx; i++)
                {
                    // Vector4 c = new Vector4(this.colors[i].r, this.colors[i].g, this.colors[i].b, 0);
                    if (this.colors != null)
                    {
                        cs[i - _draw_start_idx] = this.colors[i];
                    }
                    else if (colorbyHight == ChangeColorbyHight.Ful)
                    {
                        if (pointCloud[i].y > hi_line)
                        {
                            cs[i - _draw_start_idx] = hicolor;
                        }
                        else if (pointCloud[i].y > low_line)
                        {
                            cs[i - _draw_start_idx] = midcolor;
                        }
                        else
                        {
                            cs[i - _draw_start_idx] = lowcolor;
                        }
                    }
                    else if (colorbyHight == ChangeColorbyHight.Mono)
                    {
                        if (pointCloud[i].y > hi_line)
                        {
                            cs[i - _draw_start_idx] = PointColor;
                        }
                        else if (pointCloud[i].y > low_line)
                        {
                            cs[i - _draw_start_idx] = PointColor - new Color32(0, 0, 0, 100);
                        }
                        else
                        {
                            cs[i - _draw_start_idx] = PointColor - new Color32(0, 0, 0, 150);
                        }
                    }
                    else
                    {
                        cs[i - _draw_start_idx] = PointColor;
                    }

                }

                materialPropertyBlock.Clear();
                materialPropertyBlock.SetVectorArray("_Color", cs);
                Graphics.DrawMeshInstanced(mesh, 0, material, pointMats, pointMats.Length, materialPropertyBlock, UnityEngine.Rendering.ShadowCastingMode.Off, false);

            }
        }

        private void CreateCubeMesh()
        {
            Vector3[] vertices = {
            new Vector3 (0, 0, 0),
            new Vector3 (1, 0, 0),
            new Vector3 (1, 1, 0),
            new Vector3 (0, 1, 0),
            new Vector3 (0, 1, 1),
            new Vector3 (1, 1, 1),
            new Vector3 (1, 0, 1),
            new Vector3 (0, 0, 1),
            };

            int[] triangles = {
            0, 2, 1, //face front
			0, 3, 2,
            2, 3, 4, //face top
			2, 4, 5,
            1, 2, 5, //face right
			1, 5, 6,
            0, 7, 4, //face left
			0, 4, 3,
            5, 4, 7, //face back
			5, 7, 6,
            0, 6, 7, //face bottom
			0, 1, 6
            };

            mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.Optimize();
            mesh.RecalculateNormals();

            Resources.UnloadUnusedAssets();
            string resourcePath = "Material/PointCloud";
            material = Resources.Load<Material>(resourcePath);

            Debug.Log(material.name);

        }
    }
}
public class CoordinateConvert
{
    /*
    UnityとROSで座標系が異なる
    Unity -> ROS
        - Position: Unity(x,y,z) -> ROS(z,-x,y)
        - Quaternion: Unity(x,y,z,w) -> ROS(z,-x,y,-w)

    ROS -> Unity
        - Position: ROS(x,y,z) -> Unity(-y,z,x)
        - Quaternion: ROS(x,y,z,w) -> Unity(-y,z,x,-w)
    */
    public static Vector3 UnityToRos(Vector3 v)
    {
        return new Vector3(v.z, -v.x, v.y);
    }

    public static Quaternion UnityToRos(Quaternion q)
    {
        return new Quaternion(q.z, -q.x, q.y, -q.w);
    }

    public static Vector3 RosToUnity(Vector3 v)
    {
        return new Vector3(-v.y, v.z, v.x);
    }

    public static Quaternion RosToUnity(Quaternion q)
    {
        return new Quaternion(-q.y, q.z, q.x, -q.w);
    }
}