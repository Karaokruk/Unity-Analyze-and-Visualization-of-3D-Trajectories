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
    volatile bool keepReading = false;
    private bool isAtStartup = true;

    public String pythonPath = "python";
    public String[] fileNames;
    public int csvWriteMethod = 0;
    public int port;
    public int kmeans = 3;
    public int kmeanMethod = 1;
    public bool softKMean = true;
    public int softKMeanBeta = 1000;
    private String dataDir = "";

    System.Threading.Thread socketThread;
    private Socket listener;
    private Socket handler;
    private String assignmentsData = null;
    private String filesData = null;

    public GameObject trajectoryPrefab;
    
    void Start(){
        Application.runInBackground = true;
        dataDir += Application.dataPath + "/Resources/Datasets/";
    }

    void Update(){
        if(isAtStartup){
            if(Input.GetKeyDown(KeyCode.Space)){
                isAtStartup = false;
                StartServer();
                //DisplayTrajectories();
            }
        }
    }

    void OnGUI(){
        if(isAtStartup)
            GUI.Label(new Rect(2, 10, 150, 100), "Press Space to get data");
        else
            GUI.Label(new Rect(2, 10, 150, 100), "Acquiring data...");
        String str = "Assignments: ";
        if (assignmentsData != null)
            str += assignmentsData.ToString();
        GUI.Label(new Rect(2, 30, 1500, 100), str);
        // TODO : les trajectoires
    }

    // PYTHON LAUNCHER FUNCTION //

    void ExecPythonScript(){
        ProcessStartInfo psi = new ProcessStartInfo();
        psi.FileName = pythonPath;

        String scriptPath = Directory.GetCurrentDirectory() + "/../scripts/socket_sender.py";
        UnityEngine.Debug.Log(scriptPath);
        String pyIp = GetIpAddress().ToString();

        psi.Arguments = $"\"{scriptPath}\" \"{pyIp}\" \"{port}\"";

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
        UnityEngine.Debug.Log("Waiting for thread");
        socketThread.Join();
        UnityEngine.Debug.Log("thread over");
        DisplayTrajectories();
    }

    private String GetIpAddress() {
        IPHostEntry host;
        String localIp = "";
        host = Dns.GetHostEntry(Dns.GetHostName());
        foreach(IPAddress ip in host.AddressList){
            if(ip.AddressFamily == AddressFamily.InterNetwork)
                localIp = ip.ToString();
        }
        return localIp;
    }

    void SendMessageToPython(Byte[] bytesToSend){
        Byte[] bytes = new Byte[1024];
        handler.Send(bytesToSend);
        int bytesRec = handler.Receive(bytes);
        UnityEngine.Debug.Log(Encoding.UTF8.GetString(bytes, 0, bytesRec));
    }

    String ReceiveMessageFromPython(){
        String data = null;
        Byte[] bytes = new Byte[1024];
        Byte[] bytesToSend;
        int bytesRec = handler.Receive(bytes);

        if(bytesRec <= 0)
            return null;
        
        data += Encoding.UTF8.GetString(bytes, 0, bytesRec);
        UnityEngine.Debug.Log(data);
        if(data.IndexOf("<EOF>") > -1)
            return null;

        bytesToSend = Encoding.UTF8.GetBytes("Unity : data received");
        handler.Send(bytesToSend);
        UnityEngine.Debug.Log("Message sent back to Python");

        return data;
    }

    void NetworkCode(){
        assignmentsData = null;
        filesData = null;

        UnityEngine.Debug.Log("Ip : " + GetIpAddress().ToString() + " on port " + port.ToString());
        IPAddress[] ipArray = Dns.GetHostAddresses(GetIpAddress());
        IPEndPoint localEndPoint = new IPEndPoint(ipArray[0], port);
        listener = new Socket(ipArray[0].AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        int size = fileNames.Length;

        try{
            listener.Bind(localEndPoint);
            listener.Listen(10);

            while(true){
                keepReading = true;
                UnityEngine.Debug.Log("Waiting for Connection");
                ExecPythonScript();

                handler = listener.Accept();
                UnityEngine.Debug.Log("Client Connected");

                while(keepReading){
                    // Sending the writing method number
                    SendMessageToPython(Encoding.UTF8.GetBytes(csvWriteMethod.ToString()));
                    // Sending the number of files
                    SendMessageToPython(Encoding.UTF8.GetBytes(size.ToString()));
                    // Sending the number of kmeans
                    SendMessageToPython(Encoding.UTF8.GetBytes(kmeans.ToString()));
                    // Sending the kmean method number
                    SendMessageToPython(Encoding.UTF8.GetBytes(kmeanMethod.ToString()));
                    // Sending the soft kmean boolean
                    SendMessageToPython(Encoding.UTF8.GetBytes(softKMean.ToString()));
                    // Sending the soft kmean beta argument
                    SendMessageToPython(Encoding.UTF8.GetBytes(softKMeanBeta.ToString()));

                    foreach (String f in fileNames){
                        UnityEngine.Debug.Log("Passing file " + f + " to python...");
                        SendMessageToPython(Encoding.UTF8.GetBytes(dataDir + f));
                    }

                    // Receiving Assignment data
                    assignmentsData += ReceiveMessageFromPython();
                    filesData += ReceiveMessageFromPython();

                    if(assignmentsData == null || filesData == null){
                        keepReading = false;
                        handler.Disconnect(true);
                        break;
                    }
                    break;
                }
                break;
            }
            StopServer();
        } catch(Exception e){
            UnityEngine.Debug.Log("ERROR WHILE GETING DATA : " + e.ToString());
            StopServer();
        }
        DisplayTrajectories();
    }

    void StopServer(){
        isAtStartup = true;
        keepReading = false;
        
        if(socketThread != null){
            socketThread.Abort();
            UnityEngine.Debug.Log("Aborting socket. You must reload the Unity program to re-aquire data...");
        }
        if(handler != null && handler.Connected){
            handler.Disconnect(false);
            listener.Close();
            UnityEngine.Debug.Log("Disconnected");
        }
    }

    void OnDisable(){
        StopServer();
    }

    // Trajectories part //

    public static Color randomColorFromInt(int i)
    {
        return Color.HSVToRGB(i * 3 % 10 / 10f, 1f, 1f);
    }

    public static void PrintListOfString(List<String> l)
    {
        foreach (String str in l)
            UnityEngine.Debug.Log(str);
    }

    public static void PrintListOfListOfInt(List<List<int>> l)
    {
        foreach (List<int> list in l)
            foreach (int i in list)
                UnityEngine.Debug.Log(i);
    }
    
    private List<String> ParseListOfString(String str)
    {
        String tmp = str.Substring(1, str.Length - 2); // erase first & last characters
        tmp = String.Join("", tmp.Split(' ', '\"')); // erase all ' ' and '"'
        String[] str_array = tmp.Split(',');
        return str_array.ToList<String>();
    }

    private List<List<int>> ParseListOfListOfInts(String str)
    {
        String tmp = str.Substring(1, str.Length - 2); // erase first & last characters
        int bracket_counter = 0;
        //UnityEngine.Debug.Log(tmp);
        int size = tmp.Length;
        char[] tmp_array = new char[size];
        for (int i = 0; i < size; i++) 
        {
            if (tmp[i] == '[') bracket_counter++;
            else if (tmp[i] == ']') bracket_counter--;
            tmp_array[i] = (bracket_counter == 0 && tmp[i] == ',') ? ';' : tmp[i];
        }
        String[] str_array = (new String(tmp_array)).Split(';');
        List<List<int>> l = new List<List<int>>();
        foreach (String s in str_array)
        {
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

    public void DisplayTrajectories()
    {
        assignmentsData = "[[0], [1], [0], [2], [1]]"; // to comment
        // Assignments data parsing
        List<List<int>> assignments = ParseListOfListOfInts(assignmentsData);
        //PrintListOfListOfInt(assignments);
        
        // Trajectories files data parsing
        List<String> fileNames = ParseListOfString(filesData);
        //PrintListOfString(fileNames);

        // Create Trajectories objects
        // TODO
        int x = 0;
        GameObject t;
        for (int i = 0; i < assignments.Count; i++)
        {
            t = Instantiate(trajectoryPrefab, new Vector3(x++, assignments[i][0], 0), Quaternion.identity); // TODO
            t.transform.parent = transform;

            // upload CSV file
            CSVDataSource dataSource = t.transform.Find("[IATK] New Data Source").GetComponent<CSVDataSource>();
            //UnityEditor.AssetDatabase.ImportAsset(fileNames[1]);
            TextAsset csv = Resources.Load(fileNames[0]) as TextAsset;
            UnityEngine.Debug.Log(fileNames[0]);
            UnityEngine.Debug.Log(csv);
            dataSource.data = csv;
            // TODO

            // set up visualisation
            Visualisation dataVisualisation = t.transform.Find("[IATK] New Visualisation").GetComponent<Visualisation>();
            dataVisualisation.dataSource = dataSource;
            dataVisualisation.colour = randomColorFromInt(assignments[i][0]);
            dataVisualisation.visualisationType = IATK.AbstractVisualisation.VisualisationTypes.SCATTERPLOT;
            //dataVisualisation.visualisationReference.xDimension.Attribute = "CameraPosition.x";
            
            // TODO

            // Don't forget to update the view
        }
    }
}

