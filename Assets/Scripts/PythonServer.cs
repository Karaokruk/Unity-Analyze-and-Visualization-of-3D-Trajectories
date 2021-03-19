using System;
using System.Net;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Linq;

using UnityEngine;
using UnityEngine.UI;
using IATK;

public class PythonServer : MonoBehaviour {

    public enum w_method {
        OneCSVPerTrajectory,
        OneCSVPerCluster,
        OneCSV
    };

    public enum k_method {
        Normal,
        TranslationToOrigin,
        UsingVectors
    };

    volatile bool keepReading = false;
    private bool isAtStartup = true;

    public String pythonPath = "python";
    public int connexionPort;
    public String[] fileNames;
    //public int csvWritingMethod = 0;
    public w_method csvWritingMethod = w_method.OneCSVPerTrajectory;
    public int KMeanClusters = 3;
    //public int Kmeanmethod = 1;
    public k_method KMeanMethod = k_method.Normal;
    public bool softKMean = true;
    public float softKMeanBeta = 1000.0f;
    private String dataDir = "";

    System.Threading.Thread socketThread;
    private Socket listener;
    private Socket handler;
    private String assignmentsData = null;
    private String filesData = null;

    public GameObject trajectoryPrefab;
    
    void Start() {
        dataDir += Application.dataPath + "/Resources/Datasets/";
    }

    void Update() {
        if (isAtStartup) {
            if (Input.GetKeyDown(KeyCode.Space)) {
                isAtStartup = false;
                StartServer();
                DisplayTrajectories();
            }
        }
    }

    void OnGUI() {
        if (isAtStartup)
            GUI.Label(new Rect(2, 10, 150, 100), "Press Space to get data");
        else
            GUI.Label(new Rect(2, 10, 150, 100), "Acquiring data...");
        
        String str = "Assignments: ";
        if (assignmentsData != null)
            str += assignmentsData.ToString();
        GUI.Label(new Rect(2, 30, 1500, 100), str);
    }

    // PYTHON LAUNCHER FUNCTION //

    void ExecPythonScript() {
        ProcessStartInfo psi = new ProcessStartInfo();
        psi.FileName = pythonPath;

        String scriptPath = Directory.GetCurrentDirectory() + "/../scripts/socket_sender.py";
        UnityEngine.Debug.Log(scriptPath);
        String pyIp = GetIpAddress().ToString();

        psi.Arguments = $"\"{scriptPath}\" \"{pyIp}\" \"{connexionPort}\"";

        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;

        UnityEngine.Debug.Log("Starting Python script...");
        Process.Start(psi);
    }

    // SOCKET FUNCTIONS //

    void StartServer() {
        socketThread = new System.Threading.Thread(NetworkCode);
        socketThread.IsBackground = true;
        socketThread.Start();
        socketThread.Join();
    }

    private String GetIpAddress() {
        IPHostEntry host;
        String localIp = "";
        host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (IPAddress ip in host.AddressList) {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
                localIp = ip.ToString();
        }
        return localIp;
    }

    void SendMessageToPython(Byte[] bytesToSend){
        Byte[] bytes = new Byte[5096];
        handler.Send(bytesToSend);
        int bytesRec = handler.Receive(bytes);
        UnityEngine.Debug.Log(Encoding.UTF8.GetString(bytes, 0, bytesRec));
    }

    String ReceiveMessageFromPython(){
        String data = null;
        Byte[] bytes = new Byte[2048];
        Byte[] bytesToSend;
        int bytesRec = handler.Receive(bytes);

        if (bytesRec <= 0)
            return null;
        
        data += Encoding.UTF8.GetString(bytes, 0, bytesRec);
        UnityEngine.Debug.Log(data);
        if (data.IndexOf("<EOF>") > -1)
            return null;

        bytesToSend = Encoding.UTF8.GetBytes("Unity : data received");
        handler.Send(bytesToSend);
        UnityEngine.Debug.Log("Message sent back to Python");

        return data;
    }

    void NetworkCode(){
        assignmentsData = null;
        filesData = null;

        UnityEngine.Debug.Log("Ip : " + GetIpAddress().ToString() + " on connexionPort " + connexionPort.ToString());
        IPAddress[] ipArray = Dns.GetHostAddresses(GetIpAddress());
        IPEndPoint localEndPoint = new IPEndPoint(ipArray[0], connexionPort);
        listener = new Socket(ipArray[0].AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        int size = fileNames.Length;

        try {
            listener.Bind(localEndPoint);
            listener.Listen(10);

            while (true) {
                keepReading = true;
                UnityEngine.Debug.Log("Waiting for Connection");
                ExecPythonScript();

                handler = listener.Accept();
                UnityEngine.Debug.Log("Client Connected");

                while (keepReading) {
                    // Sending the writing method number
                    int csvMethod = 2;
                    if(csvWritingMethod == w_method.OneCSVPerTrajectory) csvMethod = 0;
                    if(csvWritingMethod == w_method.OneCSVPerCluster) csvMethod = 1;
                    SendMessageToPython(Encoding.UTF8.GetBytes(csvMethod.ToString()));
                    // Sending the number of files
                    SendMessageToPython(Encoding.UTF8.GetBytes(size.ToString()));
                    // Sending the number of kmeans
                    SendMessageToPython(Encoding.UTF8.GetBytes(KMeanClusters.ToString()));
                    // Sending the kmean method number
                    int kMean = 0;
                    if(KMeanMethod == k_method.TranslationToOrigin) kMean = 1;
                    if(KMeanMethod == k_method.UsingVectors) kMean = 2;
                    SendMessageToPython(Encoding.UTF8.GetBytes(kMean.ToString()));
                    // Sending the soft kmean boolean
                    SendMessageToPython(Encoding.UTF8.GetBytes(softKMean.ToString()));
                    // Sending the soft kmean beta argument
                    SendMessageToPython(Encoding.UTF8.GetBytes(softKMeanBeta.ToString()));

                    foreach (String f in fileNames) {
                        UnityEngine.Debug.Log("Passing file " + f + " to python...");
                        SendMessageToPython(Encoding.UTF8.GetBytes(dataDir + f));
                    }

                    // Receiving Assignment data
                    while (true) {
                        String a = ReceiveMessageFromPython();
                        assignmentsData += a;
                        if(a.Length < 1024){
                            UnityEngine.Debug.Log("Done getting Assignment.");
                            break;
                        }
                    }

                    while (true) {
                        String f = ReceiveMessageFromPython();
                        filesData += f;
                        if(f.Length < 1024){
                            UnityEngine.Debug.Log("Done getting File Data.");
                            break;
                        }
                    }

                    if (assignmentsData == null || filesData == null) {
                        keepReading = false;
                        handler.Disconnect(true);
                        break;
                    }
                    break;
                }
                break;
            }
            StopServer();
        } catch(Exception e) {
            UnityEngine.Debug.Log("ERROR WHILE GETING DATA : " + e.ToString());
            StopServer();
        }
    }

    void StopServer() {
        isAtStartup = true;
        keepReading = false;
        
        if (socketThread != null) {
            socketThread.Abort();
            UnityEngine.Debug.Log("Aborting socket. You must reload the Unity program to re-aquire data...");
        }
        if (handler != null && handler.Connected) {
            handler.Disconnect(false);
            listener.Close();
            UnityEngine.Debug.Log("Disconnected");
        }
        UnityEngine.Debug.Log(filesData);
    }

    void OnDisable() {
        StopServer();
    }

    // Trajectories part //

    public static Color randomColorFromInt(int i) {
        return Color.HSVToRGB(i * 3 % 10 / 10f, 1f, 1f);
    }

    private List<String> ParseListOfString(String str) {
        String tmp = str.Substring(1, str.Length - 2); // erase first & last characters
        tmp = String.Join("", tmp.Split(' ', '\"', '\'')); // erase specific characters
        String[] str_array = tmp.Split(',');
        return str_array.ToList<String>();
    }

    private List<List<int>> ParseListOfListOfInts(String str) {
        String tmp = str.Substring(1, str.Length - 2); // erase first & last characters
        int bracket_counter = 0;
        int size = tmp.Length;
        char[] tmp_array = new char[size];
        for (int i = 0; i < size; i++) {
            if (tmp[i] == '[') bracket_counter++;
            else if (tmp[i] == ']') bracket_counter--;
            tmp_array[i] = (bracket_counter == 0 && tmp[i] == ',') ? ';' : tmp[i];
        }
        String[] str_array = (new String(tmp_array)).Split(';');
        List<List<int>> l = new List<List<int>>();
        foreach (String s in str_array) {
            tmp = String.Join("", s.Split(' ')); // erase all ' '
            tmp = tmp.Substring(1, tmp.Length - 2); // erase first & last characters
            String[] assignementsStrings = tmp.Split(',');
            List<int> fileAssignments = new List<int>();
            foreach (String a in assignementsStrings)
                fileAssignments.Add(int.Parse(a.ToString()));
            l.Add(fileAssignments);
        }
        return l;
    }

    public void DisplayTrajectories() {
        // Assignments data parsing
        List<List<int>> assignments = ParseListOfListOfInts(assignmentsData);
        
        // Trajectories files data parsing
        List<String> fileNames = ParseListOfString(filesData);

        // Create Trajectories objects
        int nb_files = assignments.Count;
        int[] assignments_counter = new int[nb_files];
        Array.Clear(assignments_counter, 0, nb_files); // Fills array with 0s
        for (int i = 0; i < assignments.Count; i++) {
            GameObject t = Instantiate(trajectoryPrefab, new Vector3(assignments_counter[assignments[i][0]]++, assignments[i][0], 0), Quaternion.identity);
            t.transform.parent = transform;

            // upload CSV file
            CSVDataSource dataSource = t.transform.Find("[IATK] New Data Source").GetComponent<CSVDataSource>();
            dataSource.data = Resources.Load(fileNames[i]) as TextAsset;

            // set up visualisation
            Visualisation dataVisualisation = t.transform.Find("[IATK] New Visualisation").GetComponent<Visualisation>();
            dataVisualisation.dataSource = dataSource;
            dataVisualisation.colour = randomColorFromInt(assignments[i][0]);
            dataVisualisation.visualisationType = IATK.AbstractVisualisation.VisualisationTypes.SCATTERPLOT;
            dataVisualisation.xDimension = new DimensionFilter { Attribute = "x" };
            dataVisualisation.yDimension = new DimensionFilter { Attribute = "y" };
            dataVisualisation.zDimension = new DimensionFilter { Attribute = "z" };
            dataVisualisation.geometry = AbstractVisualisation.GeometryType.Lines;
            dataVisualisation.linkingDimension = "TrajectoryID";
            dataVisualisation.theVisualizationObject.UpdateVisualisation(AbstractVisualisation.PropertyType.X);
            dataVisualisation.theVisualizationObject.UpdateVisualisation(AbstractVisualisation.PropertyType.Y);
            dataVisualisation.theVisualizationObject.UpdateVisualisation(AbstractVisualisation.PropertyType.Z);
            dataVisualisation.theVisualizationObject.UpdateVisualisation(AbstractVisualisation.PropertyType.VisualisationType);
            dataVisualisation.theVisualizationObject.UpdateVisualisation(AbstractVisualisation.PropertyType.GeometryType);
            dataVisualisation.theVisualizationObject.UpdateVisualisation(AbstractVisualisation.PropertyType.LinkingDimension);
        }
    }
}

