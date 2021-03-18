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

    //C:\Users\Anton\AppData\Local\Microsoft\WindowsApps\python3.9.exe
    public String pythonPath = "python";
    public String scriptPath;
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
        dataDir += Application.dataPath + "/Datasets/";
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

        //String script = @"C:\Users\Anton\Documents\COURS\M2\PFE\Analyze-and-Visualization-of-3D-Trajectories\scripts\socket_sender.py";
        //String script = "../../../scripts/socket_sender.py";
        String py_ip = GetIpAddress().ToString();

        psi.Arguments = $"\"{scriptPath}\" \"{py_ip}\" \"{port}\"";

        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;

        var errors = "";
        var results = "";

        UnityEngine.Debug.Log("Starting Python script...");
        Process.Start(psi);

        UnityEngine.Debug.Log("Zizi");
    }

    // SOCKET FUNCTIONS //

    void StartServer() {
        socketThread = new System.Threading.Thread(NetworkCode);
        socketThread.IsBackground = true;
        socketThread.Start();
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

    void NetworkCode(){
        Byte[] bytes = new Byte[1024];
        assignmentsData = null;

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
                    bytes = new Byte[1024];
                    Byte[] bytesToSend;
                    int bytesRec;
                    // Sending the writing method number
                    bytesToSend = Encoding.UTF8.GetBytes(csvWriteMethod.ToString());
                    handler.Send(bytesToSend);
                    bytesRec = handler.Receive(bytes);
                    UnityEngine.Debug.Log(Encoding.UTF8.GetString(bytes, 0, bytesRec));
                    // Sending the number of files
                    bytesToSend = Encoding.UTF8.GetBytes(size.ToString());
                    handler.Send(bytesToSend);
                    bytesRec = handler.Receive(bytes);
                    UnityEngine.Debug.Log(Encoding.UTF8.GetString(bytes, 0, bytesRec));
                    // Sending the number of kmeans
                    bytesToSend = Encoding.UTF8.GetBytes(kmeans.ToString());
                    handler.Send(bytesToSend);
                    bytesRec = handler.Receive(bytes);
                    UnityEngine.Debug.Log(Encoding.UTF8.GetString(bytes, 0, bytesRec));
                    // Sending the kmean method number
                    bytesToSend = Encoding.UTF8.GetBytes(kmeanMethod.ToString());
                    handler.Send(bytesToSend);
                    bytesRec = handler.Receive(bytes);
                    UnityEngine.Debug.Log(Encoding.UTF8.GetString(bytes, 0, bytesRec));
                    // Sending the soft kmean boolean
                    bytesToSend = Encoding.UTF8.GetBytes(softKMean.ToString());
                    handler.Send(bytesToSend);
                    bytesRec = handler.Receive(bytes);
                    UnityEngine.Debug.Log(Encoding.UTF8.GetString(bytes, 0, bytesRec));
                    // Sending the soft kmean beta argument
                    bytesToSend = Encoding.UTF8.GetBytes(softKMeanBeta.ToString());
                    handler.Send(bytesToSend);
                    bytesRec = handler.Receive(bytes);
                    UnityEngine.Debug.Log(Encoding.UTF8.GetString(bytes, 0, bytesRec));

                    foreach (String f in fileNames){
                        UnityEngine.Debug.Log("Passing file " + f + " to python...");
                        bytesToSend = Encoding.UTF8.GetBytes(dataDir + f);
                        handler.Send(bytesToSend);
                        bytesRec = handler.Receive(bytes);
                        UnityEngine.Debug.Log(Encoding.UTF8.GetString(bytes, 0, bytesRec));
                    }

                    bytesRec = handler.Receive(bytes);

                    if(bytesRec <= 0){
                        keepReading = false;
                        handler.Disconnect(true);
                        break;
                    }
                    assignmentsData += Encoding.UTF8.GetString(bytes, 0, bytesRec);
                    UnityEngine.Debug.Log(assignmentsData);
                    if(assignmentsData.IndexOf("<EOF>") > -1)
                        break;

                    bytesToSend = Encoding.UTF8.GetBytes("Unity : data received");
                    handler.Send(bytesToSend);
                    UnityEngine.Debug.Log("Message sent back to Python");
                    break;
                    //System.Threading.Thread.Sleep(1);
                }
                //System.Threading.Thread.Sleep(1);
                break;
            }
        } catch(Exception e){
            UnityEngine.Debug.Log("ERROR WHILE GETING DATA : " + e.ToString());
        }
                        
        if(handler != null && handler.Connected){
            handler.Disconnect(false);
            listener.Close();
            UnityEngine.Debug.Log("Disconnected");
        }
        StopServer();
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
        UnityEngine.Debug.Log(tmp);
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
        assignmentsData = "[[0], [1], [2], [3], [4], [5], [6], [7], [8]]"; // to comment
        filesData = "[\"participant7trial1-ontask-quarter\", \"Participant_7_HeadPositionLog\"]"; // to comment

        // Assignments data parsing
        List<List<int>> assignments = ParseListOfListOfInts(assignmentsData);
        PrintListOfListOfInt(assignments);
        
        // Trajectories files data parsing
        List<String> fileNames = ParseListOfString(filesData);
        PrintListOfString(fileNames);

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
            UnityEngine.Debug.Log(dataSource);
            // TODO

            // set up visualisation
            Visualisation dataVisualisation = t.transform.Find("[IATK] New Visualisation").GetComponent<Visualisation>();
            dataVisualisation.dataSource = dataSource;
            dataVisualisation.colour = randomColorFromInt(assignments[i][0]);
            // TODO
        }
    }
}

