using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Newtonsoft.Json;
using System.Text.Json;
using RestSharp;
using Newtonsoft.Json.Linq;

namespace Tables
{
    /// <summary>
    /// 主窗口，实现了INotifyPropertyChanged接口以支持数据绑定和属性更改通知。
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region Static Resources
        /// <summary>
        /// 任务文件名，用于存储和加载任务数据。
        /// </summary>
        public static string fileName = "tasks.json";
        #endregion

        #region Attributes
        /// <summary>
        /// 任务集合，存储所有任务。
        /// </summary>
        private ObservableCollection<Task> tasks;
        public ObservableCollection<Task> Tasks
        {
            get { return tasks; }
            set
            {
                if (value != tasks)
                {
                    tasks = value;
                    OnPropertyChanged("Tasks");
                }
            }
        }

        /// <summary>
        /// 当前选中的任务。
        /// </summary>
        private Task selectedTask;
        public Task SelectedTask
        {
            get { return selectedTask; }
            set
            {
                if (value != selectedTask)
                {
                    selectedTask = value;
                    OnPropertyChanged("SelectedTask");
                }
            }
        }
        #endregion

        #region Constructors
        /// <summary>
        /// MainWindow构造函数，初始化窗口并加载任务数据。
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            Tasks = new ObservableCollection<Task>();
            SelectedTask = null;
            DataContext = this;
            DeserializeTasks();
        }
        #endregion

        #region Event Handling
        /// <summary>
        /// 添加按钮点击事件处理程序，将新任务添加到任务集合中并保存数据。
        /// </summary>
        private void buttonAdd_Click(object sender, RoutedEventArgs e)
        {
            Tasks.Add(new Task(textBox.Text));
            SerializeTasks();
        }

        /// <summary>
        /// 删除按钮点击事件处理程序，删除选中的任务并保存数据。
        /// </summary>
        private void buttonDelete_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedTask != null)
            {
                int index = -1;
                for (int i = 0; i < Tasks.Count; i++)
                {
                    if (Tasks[i].task.Equals(SelectedTask.task))
                    {
                        index = i;
                    }
                }
                if (index != -1)
                {
                    Tasks.RemoveAt(index);
                    OnPropertyChanged("Tasks");
                }
            }
        }
        #endregion

        #region PropertyChangedNotifier
        /// <summary>
        /// 属性更改事件处理程序，用于通知绑定到属性的控件更新。
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }
        #endregion

        #region Serialization
        /// <summary>
        /// 序列化任务集合到JSON字符串并保存到文件。
        /// </summary>
        public void SerializeTasks()
        {
            string json = JsonConvert.SerializeObject(Tasks);

            using (FileStream fileStream = new FileStream("tasks.json", FileMode.Create))
            using (StreamWriter writer = new StreamWriter(fileStream))
            {
                writer.Write(json);
            }
        }

        /// <summary>
        /// 从文件加载JSON字符串并反序列化为任务集合。
        /// </summary>
        public void DeserializeTasks()
        {
            if (File.Exists(fileName))
            {
                using (FileStream fileStream = new FileStream(fileName, FileMode.Open))
                using (StreamReader reader = new StreamReader(fileStream))
                {
                    string json = reader.ReadToEnd();
                    Tasks = JsonConvert.DeserializeObject<ObservableCollection<Task>>(json);
                }
            }
        }
        #endregion

        /// <summary>
        /// 向DashScope API发送请求，生成基于当前任务列表的建议文本。
        /// </summary>
        /// <param name="user_content">用户输入的内容。</param>
        /// <returns>API返回的建议文本。</returns>
        private string Send_Qwen(string user_content)
        {
            string api_key = "sk-957abfc22958436e99fe4157216528b3";
            string url = "https://dashscope.aliyuncs.com/api/v1/services/aigc/text-generation/generation";
            var client = new RestClient(url);
            var request = new RestRequest(Method.POST);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Authorization", $"Bearer {api_key}");
            var requestBody = new
            {
                model = "qwen-turbo",
                input = new
                {
                    messages = new[]
                    {
                        new { role = "system", content = "You are a helpful assistant." },
                        new { role = "user", content = user_content }
                    }
                },
                parameters = new { result_format = "message" }
            };

            // 使用JsonSerializer.Serialize序列化请求体
            request.AddParameter("application/json", System.Text.Json.JsonSerializer.Serialize(requestBody), ParameterType.RequestBody);
            IRestResponse response = client.Execute(request);
            return response.Content;
        }

        /// <summary>
        /// 解析API响应，提取建议文本。
        /// </summary>
        /// <param name="Qwen_Response">API的原始响应。</param>
        /// <returns>提取出的建议文本。</returns>
        private string Analyse_Qwen_Response(string Qwen_Response)
        {
            JObject jsonObject = JObject.Parse(Qwen_Response);
            string content = jsonObject["output"]["choices"][0]["message"]["content"].ToString();
            return content;
        }

        /// <summary>
        /// 处理生成建议的按钮点击事件。
        /// </summary>
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string prompt = Generate_Prompt();
            string response_from_API = Send_Qwen(prompt).ToString();
            string response_toShow = Analyse_Qwen_Response(response_from_API);
            suggestion_TextBox.Text = response_toShow;
        }

        /// <summary>
        /// 根据当前任务列表生成用于API输入的prompt。
        /// </summary>
        /// <returns>生成的prompt字符串。</returns>
        private string Generate_Prompt()
        {
            string json = JsonConvert.SerializeObject(Tasks);
            string prompt = "我是一个在校大学生，下面这一段json文本展示了我现有的任务清单（即To-do List）。请根据我的任务清单，给我一些关于我接下来的学习规划的建议。\n";
            prompt += "```json" + json + "\n" + "```";
            return prompt;
        }
    }
}