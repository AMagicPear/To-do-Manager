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
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region Static Resources
        public static string fileName = "tasks.json";
        #endregion

        #region Attributes
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
        private void buttonAdd_Click(object sender, RoutedEventArgs e)
        {
            Tasks.Add(new Task(textBox.Text));
            SerializeTasks();
        }

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
        public void SerializeTasks()
        {
            string json = JsonConvert.SerializeObject(Tasks);

            using (FileStream fileStream = new FileStream("tasks.json", FileMode.Create))
            using (StreamWriter writer = new StreamWriter(fileStream))
            {
                writer.Write(json);
            }
        }

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
        private string Analyse_Qwen_Response(string Qwen_Response)
        {
            JObject jsonObject = JObject.Parse(Qwen_Response);
            string content = jsonObject["output"]["choices"][0]["message"]["content"].ToString();
            return content;
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string prompt = Generate_Prompt();
            string response_from_API = Send_Qwen(prompt).ToString();
            string response_toShow = Analyse_Qwen_Response(response_from_API);
            suggestion_TextBox.Text = response_toShow;
        }

        private string Generate_Prompt()
        {
            string json = JsonConvert.SerializeObject(Tasks);
            string prompt = "我是一个在校大学生，下面这一段json文本展示了我现有的任务清单（即To-do List）。请根据我的任务清单，给我一些关于我接下来的学习规划的建议。\n";
            prompt += "```json" + json + "\n" + "```";
            return prompt;
        }
    }
}