using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using System.Xml;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace FairyXML2Lua
{
    public partial class Form1 : Form
    {
        /// <summary>
        /// exe启动路径
        /// </summary>
        string startUpPath = "";
        /// <summary>
        /// 包路径
        /// </summary>
        string packagePath = "";
        /// <summary>
        /// 包名
        /// </summary>
        string packageName = "";
        public Form1()
        {
            InitializeComponent();

            textBox1.Text = Properties.Settings.Default.recentPackage;
            textBox2.Text = Properties.Settings.Default.savePath;

            startUpPath = Assembly.GetExecutingAssembly().Location;
            startUpPath = Path.GetDirectoryName(startUpPath);
            //test 
            //startUpPath = @"C:\Project\FF_FairyGui\assets";
            startUpPath = Path.Combine(startUpPath, "assets");
            //ViewAllFolders(startUpPath);
            InitPackNameList();
        }
        /// <summary>
        /// 读取包名列表 自动赋值
        /// </summary>
        void InitPackNameList()
        {
            comboBox1.Items.Clear();

            DirectoryInfo dir = new DirectoryInfo(startUpPath);
            foreach (FileSystemInfo fInfo in dir.GetFileSystemInfos())
            {
                if (fInfo is DirectoryInfo)
                {
                    var folderName = Path.GetFileNameWithoutExtension(fInfo.Name);
                    comboBox1.Items.Add(folderName);
                }
            }
        }
        void ViewAllFolders(string startUpPath)
        {
            foreach (string pathName in Directory.EnumerateDirectories(startUpPath))
            {
                Console.WriteLine(Path.GetFileName(pathName));
            }

        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(textBox1.Text))
            {
                MessageBox.Show("请填写fairy 的包名");
                return;
            }
            if (string.IsNullOrEmpty(textBox2.Text))
            {
                MessageBox.Show("请填写导出LUA文件的路径");
                return;
            }
            var folder = Path.Combine(textBox2.Text, textBox1.Text);
            string[] filePaths = Directory.GetFiles(folder);
            foreach (string filepath in filePaths)
            {
                File.Delete(filepath);
            }
            FindXMLFiles(textBox1.Text, textBox2.Text);
        }
        /// <summary>
        /// 根据包名 找到对应目录里面需要转lua的xml文件列表
        /// </summary>
        /// <param name="packageName"></param>
        /// <param name="saveLuaPath"></param>
        void FindXMLFiles(string packageName, string saveLuaPath)
        {
            //保存最近的输入记录
            Properties.Settings.Default.recentPackage = packageName;
            Properties.Settings.Default.savePath = saveLuaPath;
            Properties.Settings.Default.Save();

            packagePath = Path.Combine(startUpPath, packageName);
            string pacXMLPath = Path.Combine(packagePath, "package.xml");
            if (File.Exists(pacXMLPath) == false)
            {
                MessageBox.Show(packagePath + "找不到 package.xml");
                return;
            }
            List<string> compNameList = new List<string>();
            XmlDocument doc = new XmlDocument();
            doc.Load(pacXMLPath);
            XmlElement root = doc.DocumentElement;

            XmlNodeList resourceNodes = root.SelectNodes("/packageDescription/resources/component");
            foreach (XmlNode node in resourceNodes)
            {
                if (node.Name == "component")
                {
                    string fileName = node.Attributes["name"].Value;
                    if (!fileName.EndsWith("_NOLUA.xml"))
                    {
                        string path = "";
                        if (node.Attributes["path"] != null)
                            path = Path.Combine(node.Attributes["path"].Value, node.Attributes["name"].Value);
                        else
                            path = node.Attributes["name"].Value;
                        compNameList.Add(path);
                    }
                }
            }

            saveLuaPath = Path.Combine(saveLuaPath, packageName);
            foreach (string compName in compNameList)
            {
                string path = Path.Combine(packagePath, compName);
                ConvertToLuaCode(path, saveLuaPath);
            }
            MessageBox.Show("导出成功!");
            //ProcessStartInfo startInfo = new ProcessStartInfo(saveLuaPath, "explorer.exe");
            //Process.Start(startInfo);

        }
        /// <summary>
        /// 是默认物体名 (默认物体不导出)
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        bool IsNoExportName(string name)
        {
            return Regex.IsMatch(name, @"^n[0-9][0-9]*");
        }
        /// <summary>
        /// 一个fairy xml转换成lua代码
        /// </summary>
        /// <param name="inPath">xml文件路径: /组件/TestUI.xml</param>
        /// <param name="outPath">导出路径: \Assets\Scripts\LuaScripts\UI_LUA</param>
        void ConvertToLuaCode(string inPath, string outPath)
        {
            Dictionary<string, int> objIndexDic = new Dictionary<string, int>();
            Dictionary<string, int> ctrlIndexDic = new Dictionary<string, int>();
            Dictionary<string, int> animIndexDic = new Dictionary<string, int>();

            //读取XML文件
            XmlDocument doc = new XmlDocument();
            //正斜杠分离 转换成windows的反斜杠路径
            string[] pathes = inPath.Split('/');
            string tempPath = packagePath;
            foreach (string folder in pathes)
            {
                tempPath = Path.Combine(tempPath, folder);
            }
            doc.Load(tempPath);
            //string packageName = Path.GetDirectoryName(inPath);
            //packageName = Path.GetFileNameWithoutExtension(packageName);
            string fileName = Path.GetFileNameWithoutExtension(inPath);
            fileName = "UI_" + fileName;

            //XML里面找到内容
            XmlElement root = doc.DocumentElement;
            int ctrlCount = 0;
            int animCount = 0;
            foreach (XmlNode child in root.ChildNodes)
            {
                if (child.Name == "controller")
                {
                    ctrlIndexDic.Add(child.Attributes["name"].Value, ctrlCount);
                    ctrlCount++;
                }
                if (child.Name == "transition")
                {
                    animIndexDic.Add(child.Attributes["name"].Value, animCount);
                    animCount++;
                }
            }
            XmlNodeList listNodes = root.SelectNodes("/component/displayList");
            int count = 0;

            /*
             * 规则: 普通组 不计数 不导出 
                     物体 计数 起名则导出 
                     高级组 计数  起名则导出 
             */
            foreach (XmlNode node in listNodes)
            {
                foreach (XmlNode child in node.ChildNodes)
                {
                    if (child.Name == "group")
                    {
                        if (child.Attributes["advanced"] == null)// 普通组 不计数 不导出 
                            continue;
                    }
                    string name = child.Attributes["name"].Value;
                    if (!string.IsNullOrEmpty(name) && !objIndexDic.ContainsKey(name) && !IsNoExportName(name))
                    {
                        objIndexDic.Add(name, count);
                    }
                    count++;
                }
            }
            #region emmyLua 提示信息
            string firstLine = $"---@class {fileName} : FairyGUI.GComponent\n";
            string headerContent = "";
            foreach (var pair in objIndexDic)
            {
                string typeString = GetTypeByName(pair.Key);
                headerContent += $"---@field public {pair.Key} {typeString}\n";
            }
            foreach (var pair in ctrlIndexDic)
            {
                headerContent += $"---@field public {pair.Key} FairyGUI.Controller\n";
            }
            foreach (var pair in animIndexDic)
            {
                headerContent += $"---@field public {pair.Key} FairyGUI.Transition\n";
            }
            #endregion

            //拼LUA代码
            string template =
            "{0} = {1}\nfunction {2}.FindChild(view,ui)\n ui.gObject=view\n {3}end";
            string content = "";
            foreach (var pair in objIndexDic)
            {
                content += $"ui.{pair.Key}=view:GetChildAt({pair.Value})\n";
            }
            foreach (var pair in ctrlIndexDic)
            {
                content += $"ui.{pair.Key}=view:GetControllerAt({pair.Value})\n";
            }
            foreach (var pair in animIndexDic)
            {
                content += $"ui.{pair.Key}=view:GetTransitionAt({pair.Value})\n";
            }
            template = string.Format(template, fileName, @"{}", fileName, content);
            string finalContent = firstLine + headerContent + template;
            //写入目标位置
            string dir = Path.Combine(Properties.Settings.Default.savePath, packageName);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            string _outPath = Path.Combine(outPath, packageName, fileName);
            if (!Directory.Exists(outPath))
            {
                Directory.CreateDirectory(outPath);
            }
            _outPath = Path.ChangeExtension(_outPath, "lua");
            File.WriteAllText(_outPath, finalContent);
        }
        string GetTypeByName(string compName)
        {
            if (compName.StartsWith("txt_"))
                return "FairyGUI.GTextField";

            if (compName.StartsWith("list_"))
                return "FairyGUI.GList";

            if (compName.StartsWith("btn_") || compName.Contains("button"))
                return "FairyGUI.GButton";

            if (compName.StartsWith("input_"))
                return "FairyGUI.InputTextField";

            if (compName.StartsWith("cbox_"))
                return "FairyGUI.GComboBox";

            if (compName.StartsWith("tree_"))
                return "FairyGUI.GTree";

            return "FairyGUI.GComponent";
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            textBox1.Text = this.comboBox1.SelectedItem.ToString();
        }


        HashSet<string> fileNameSet = new HashSet<string>();
        string fileNameHint = "";
        string charHint = "";
        /// <summary>
        /// 查询重名文件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button2_Click(object sender, EventArgs e)
        {
            string startFolder = Assembly.GetExecutingAssembly().Location;
            //test 
            //startFolder = @"C:\Project\FF_FairyGui\FairyXML2Lua.exe";

            startFolder = Path.GetDirectoryName(startFolder);
            startFolder = Path.Combine(startFolder, "assets");
            fileNameSet.Clear();
            fileNameHint = "";
            charHint = "";

            FillFileNames(startFolder);

            if (string.IsNullOrEmpty(fileNameHint))
                MessageBox.Show("没有重名组件");
            else
                MessageBox.Show(fileNameHint);


            if (!string.IsNullOrEmpty(charHint))
                MessageBox.Show(charHint);
        }
        void FillFileNames(string folder)
        {
            DirectoryInfo dir = new DirectoryInfo(folder);
            foreach (FileSystemInfo fInfo in dir.GetFileSystemInfos())
            {
                if (fInfo is DirectoryInfo)
                {
                    FillFileNames(fInfo.FullName);
                }
                else
                {
                    if (fInfo.Name != "package.xml" && fInfo.Extension == ".xml")
                    {
                        var fileName = Path.GetFileNameWithoutExtension(fInfo.Name);
                        if (fileNameSet.Contains(fileName))
                        {
                            fileNameHint += $"重名xml文件 {fInfo.FullName}\n";
                        }
                        else
                            fileNameSet.Add(fileName);

                        if (fileName.Contains("(") || fileName.Contains(")"))
                        {
                            charHint += $"带括号的文件名 {fInfo.FullName}\n";
                        }
                    }
                }
            }
        }
        private void button3_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(textBox1.Text))
            {
                MessageBox.Show("请填写fairy 的包名");
                return;
            }
            string startFolder = Assembly.GetExecutingAssembly().Location;
            //test 
            //startFolder = @"C:\Project\FF_FairyGui\FairyXML2Lua.exe";

            startFolder = Path.GetDirectoryName(startFolder);
            startFolder = Path.Combine(startFolder, "assets", textBox1.Text);
            listPNGs = new List<PictureWithArea>();

            FindInPackage_bigPic(startFolder);
            listPNGs.Sort((a, b) => b.pixel_size.CompareTo(a.pixel_size));

            string pngRank = "";
            foreach (var pwa in listPNGs)
            {
                string line = $"size = {pwa.pixel_size} height={pwa.height} width = {pwa.width}  path= {pwa.fullName}\n";
                pngRank += line;
            }

            File.WriteAllText(@"D:\大图片列表.txt", pngRank);
            MessageBox.Show(@"结果保存在 D:\大图片列表.txt");
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(textBox1.Text))
            {
                MessageBox.Show("请填写fairy 的包名");
                return;
            }
            string startFolder = Assembly.GetExecutingAssembly().Location;
            //test 
            //startFolder = @"C:\Project\FF_FairyGui\FairyXML2Lua.exe";

            startFolder = Path.GetDirectoryName(startFolder);
            startFolder = Path.Combine(startFolder, "assets", textBox1.Text);
            listPNGs = new List<PictureWithArea>();

            FindXMLPackage(startFolder);

            //查詢結束后查询一次拿到的数据判断
            if (listPicUseSource != null)
            {
                foreach (var picUse in listPicUseSource)
                {
                    if (!listComUseSource.ContainsKey(picUse.imageId))
                    {
                        listPicNeedDel.Add(picUse);
                    }
                }
            }

            //将所有数据打印一遍
            string pngRank = "";
            foreach (var pwa in listPicNeedDel)
            {
                string line = $"name = {pwa.fullName} imageId={pwa.imageId}" + "\n";
                pngRank += line;
            }

            File.WriteAllText(@"D:\应该删除的图片.txt", pngRank);
            MessageBox.Show(@"结果保存在 D:\应该删除的图片.txt");

        }

        class PictureWithArea
        {
            public string fullName;
            public double pixel_size;
            public int height;
            public int width;
        }

        class PictureSourceWithId
        {
            public string fullName;
            public string imageId;
        }

        List<PictureWithArea> listPNGs;
        void FindInPackage_bigPic(string folder)
        {
            DirectoryInfo dir = new DirectoryInfo(folder);
            foreach (FileSystemInfo fInfo in dir.GetFileSystemInfos())
            {
                if (fInfo is DirectoryInfo)
                {
                    FindInPackage_bigPic(fInfo.FullName);
                }
                else
                {
                    //图片文件遍历 找尺寸
                    if (fInfo.Extension == ".png" || fInfo.Extension == ".jpg")
                    {
                        System.Drawing.Image img = System.Drawing.Image.FromFile(fInfo.FullName);

                        PictureWithArea pwa = new PictureWithArea();
                        pwa.fullName = fInfo.FullName;
                        pwa.height = img.Height;
                        pwa.width = img.Width;
                        pwa.pixel_size = Math.Floor(pwa.height * pwa.width / 1000f);

                        listPNGs.Add(pwa);
                    }
                }
            }
        }

        List<PictureSourceWithId> listPicUseSource;  //使用中图片
        List<PictureSourceWithId> listPicNeedDel;    //要删除的图片
        Dictionary<string, int> listComUseSource;    //引用的图片
        void FindXMLPackage(string folder)
        {
            if (listPicUseSource == null)
                listPicUseSource = new List<PictureSourceWithId>();
            else
                listPicUseSource.Clear();

            if (listComUseSource == null)
                listComUseSource = new Dictionary<string, int>();
            else
                listComUseSource.Clear();

            if (listPicNeedDel == null)
                listPicNeedDel = new List<PictureSourceWithId>();
            else
                listPicNeedDel.Clear();

            FindAllImageUse(folder);

        }
        string shw = "";

        //查詢所有圖片資源的使用情況
        void FindAllImageUse(string folder)
        {
            DirectoryInfo dir = new DirectoryInfo(folder);
            foreach (FileSystemInfo fInfo in dir.GetFileSystemInfos())
            {
                if (fInfo is DirectoryInfo)
                {
                    FindAllImageUse(fInfo.FullName);
                }
                else
                {
                    if (fInfo.Extension == ".xml")
                    {
                        if (fInfo.Name == "package.xml")
                        {
                            shw = shw + fInfo.Name + "\\  ";
                            List<string> compNameList = new List<string>();
                            XmlDocument doc = new XmlDocument();
                            doc.Load(fInfo.FullName);
                            XmlElement root = doc.DocumentElement;

                            XmlNodeList resourceNodes = root.SelectNodes("/packageDescription/resources");
                            shw = shw + resourceNodes.Count + "\\  ";

                            foreach (XmlNode node in resourceNodes)
                            {
                                shw = shw + node.ChildNodes.Count + "\\  ";

                                foreach (XmlNode child in node.ChildNodes)
                                {
                                    string fileName = child.Attributes["id"].Value;
                                    var exported = child.Attributes["exported"];
                                    if (child.Name == "image")
                                    {
                                        if (exported == null)
                                        {
                                            PictureSourceWithId pwa = new PictureSourceWithId();
                                            pwa.fullName = child.Attributes["path"].Value + child.Attributes["name"].Value;
                                            pwa.imageId = fileName;
                                            listPicUseSource.Add(pwa);
                                        }
                                    }
                                    else
                                    {
                                        if (child.Attributes["src"] != null)
                                        {
                                            listComUseSource[child.Attributes["src"].Value] = 1;
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            shw = shw + fInfo.Extension + "\\  ";
                            List<string> compNameList = new List<string>();
                            XmlDocument doc = new XmlDocument();
                            doc.Load(fInfo.FullName);
                            XmlElement root = doc.DocumentElement;

                            XmlNodeList resourceNodes = root.SelectNodes("/component/displayList");
                            shw = shw + resourceNodes.Count + "\\  ";

                            foreach (XmlNode node in resourceNodes)
                            {
                                shw = shw + node.ChildNodes.Count + "\\  ";
                                foreach (XmlNode child in node.ChildNodes)
                                {
                                    if (child.Attributes["src"] != null)
                                    {
                                        listComUseSource[child.Attributes["src"].Value] = 1;
                                    }
                                }

                            }
                        }

                    }
                }
            }

        }

        /// <summary>
        /// 批量操作的次数
        /// </summary>
        int batch_count = 0;
        private void btn_UnBold_Click(object sender, EventArgs e)
        {

            if (string.IsNullOrEmpty(textBox1.Text))
            {
                MessageBox.Show("请填写fairy 的包名");
                return;
            }

            FindFilesForUnbold(textBox1.Text, 1);

        }

        /// <summary>
        /// 根据包名 找到对应目录里面需要转lua的xml文件列表
        /// </summary>
        /// <param name="packageName"></param>
        /// <param name="type">1=取消文本粗体 2=文本描边改成0.15</param>
        void FindFilesForUnbold(string packageName, int type)
        {
            //保存最近的输入记录
            Properties.Settings.Default.recentPackage = packageName;
            Properties.Settings.Default.Save();
            packagePath = Path.Combine(startUpPath, packageName);
            string pacXMLPath = Path.Combine(packagePath, "package.xml");
            if (File.Exists(pacXMLPath) == false)
            {
                MessageBox.Show(packagePath + "找不到 package.xml");
                return;
            }
            List<string> compNameList = new List<string>();
            XmlDocument doc = new XmlDocument();
            doc.Load(pacXMLPath);
            XmlElement root = doc.DocumentElement;

            XmlNodeList resourceNodes = root.SelectNodes("/packageDescription/resources/component");
            foreach (XmlNode node in resourceNodes)
            {
                if (node.Name == "component")
                {
                    string fileName = node.Attributes["name"].Value;

                    string path = "";
                    if (node.Attributes["path"] != null)
                        path = Path.Combine(node.Attributes["path"].Value, node.Attributes["name"].Value);
                    else
                        path = node.Attributes["name"].Value;
                    compNameList.Add(path);
                }
            }
            batch_count = 0;
            foreach (string compName in compNameList)
            {
                string path = Path.Combine(packagePath, compName);
                UnBoldTextInFiles(path, type);
            }
            if (type == 1)
                MessageBox.Show($"取消文本粗体完成 操作次数={batch_count}");
            else if (type == 2)
                MessageBox.Show($"描边改成0.15完成 操作次数={batch_count}");

            //ProcessStartInfo startInfo = new ProcessStartInfo(saveLuaPath, "explorer.exe");
            //Process.Start(startInfo);
        }
        void UnBoldTextInFiles(string inPath, int type)
        {
            //读取XML文件
            XmlDocument doc = new XmlDocument();
            //正斜杠分离 转换成windows的反斜杠路径
            string[] pathes = inPath.Split('/');
            string tempPath = packagePath;
            foreach (string folder in pathes)
            {
                tempPath = Path.Combine(tempPath, folder);
            }
            doc.Load(tempPath);

            //XML里面找到内容
            XmlElement root = doc.DocumentElement;
            XmlNodeList listNodes = root.SelectNodes("/component/displayList");

            foreach (XmlNode node in listNodes)
            {
                foreach (XmlNode child in node.ChildNodes)
                {
                    if (child.Name == "text" || child.Name == "richtext")
                    {
                        if (type == 1)
                        {
                            if (child.Attributes["bold"] != null)
                            {
                                if (child.Attributes["bold"].Value == "true")
                                {
                                    child.Attributes["bold"].Value = "false";
                                    batch_count++;
                                }
                            }
                        }
                        else if (type == 2)
                        {
                            if (child.Attributes["strokeColor"] != null &&
                                child.Attributes["strokeSize"] == null)
                            {
                                var newAttr = doc.CreateAttribute("strokeSize");
                                newAttr.Value = "0.15";
                                child.Attributes.Append(newAttr);
                            }
                            else if (child.Attributes["strokeSize"] != null)
                            {
                                child.Attributes["strokeSize"].Value = "0.15";
                                batch_count++;
                            }
                        }
                    }
                }
            }
            doc.Save(tempPath);
        }

        private void button5_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(textBox1.Text))
            {
                MessageBox.Show("请填写fairy 的包名");
                return;
            }

            FindFilesForUnbold(textBox1.Text, 2);
        }
    }
}
