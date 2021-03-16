using System;
using System.Net;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Linq;

using UnityEngine;
using UnityEngine.UI;

public class PythonServer : MonoBehaviour {
    volatile bool keepReading = false;
    private bool isAtStartup = true;

    public int port;
    public String[] fileNames;
    private String dataDir = "";
    //public List<String> fileNamesList = new List<String>();

    System.Threading.Thread socketThread;
    private Socket listener;
    private Socket handler;
    private String assignmentsData = null;
    private String filesData = null;
    
    void Start(){
        Application.runInBackground = true;
        dataDir += Application.dataPath + "/Datasets/";
    }

    void Update(){
        if(isAtStartup){
            if(Input.GetKeyDown(KeyCode.Space)){
                isAtStartup = false;
                StartServer();
                DisplayTrajectories();
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

        Debug.Log("Ip : " + GetIpAddress().ToString() + " on port " + port.ToString());
        IPAddress[] ipArray = Dns.GetHostAddresses(GetIpAddress());
        IPEndPoint localEndPoint = new IPEndPoint(ipArray[0], port);

        listener = new Socket(ipArray[0].AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        int size = fileNames.Length;

        try{
            listener.Bind(localEndPoint);
            listener.Listen(10);

            while(true){
                keepReading = true;
                Debug.Log("Waiting for Connection");

                handler = listener.Accept();
                Debug.Log("Client Connected");

                while(keepReading){
                    bytes = new Byte[1024];
                    Byte[] bytesToSend;
                    int bytesRec;

                    bytesToSend = Encoding.UTF8.GetBytes(size.ToString());
                    handler.Send(bytesToSend);
                    bytesRec = handler.Receive(bytes);

                    foreach (String f in fileNames){
                        Debug.Log("Passing file " + f + " to python...");
                        bytesToSend = Encoding.UTF8.GetBytes(dataDir + f);
                        handler.Send(bytesToSend);
                        bytesRec = handler.Receive(bytes);
                        Debug.Log(Encoding.UTF8.GetString(bytes, 0, bytesRec));
                    }

                    bytesRec = handler.Receive(bytes);
                    //Debug.Log("Received from Server");

                    if(bytesRec <= 0){
                        keepReading = false;
                        handler.Disconnect(true);
                        break;
                    }
                    assignmentsData += Encoding.UTF8.GetString(bytes, 0, bytesRec);
                    Debug.Log(assignmentsData);
                    if(assignmentsData.IndexOf("<EOF>") > -1)
                        break;

                    bytesToSend = Encoding.UTF8.GetBytes("Unity : data received");

                    handler.Send(bytesToSend);
                    Debug.Log("Message sent back to Python");

                    System.Threading.Thread.Sleep(1);
                }
                System.Threading.Thread.Sleep(1);
            }
        } catch(Exception e){
            Debug.Log("ERROR WHILE GETING DATA : " + e.ToString());
        }
        StopServer();
    }

    void StopServer(){
        isAtStartup = true;
        keepReading = false;
        
        if(socketThread != null){
            socketThread.Abort();
            Debug.Log("Aborting socket. You must reload the Unity program to re-aquire data...");
        }
        
        if(handler != null && handler.Connected){
            handler.Disconnect(false);
            Debug.Log("Disconnected");
        }
    }

    void OnDisable(){
        StopServer();
    }

    // Trajectories part //

    public static void PrintStringArray(String[] str_array)
    {
        foreach (String str in str_array)
            Debug.Log(str);
    }

    public static void PrintListOfListOfInt(List<List<int>> l)
    {
        foreach (List<int> list in l)
            foreach (int i in list)
                Debug.Log(i);
    }
    
    private String[] ParseStringArray(String str)
    {
        String tmp = str.Substring(1, str.Length - 2); // erase first & last characters
        tmp = String.Join("", tmp.Split(' ', '\"')); // erase all ' ' and '"'
        print(tmp);
        String[] str_array = tmp.Split(',');
        return str_array;
    }

    private List<List<int>> ParseListOfListOfInts(String str)
    {
        String tmp = str.Substring(1, str.Length - 2); // erase first & last characters
        List<List<int>> a = new List<List<int>>();
        Stack bracketCounter;
        //int.Parse(assignment);
        return a;
    }

    public void DisplayTrajectories()
    {
        assignmentsData = "[[0], [1]]"; // to comment
        filesData = "[\"participant7trial1-ontask-quarter\", \"Participant_7_HeadPositionLog\"]"; // to comment

        /*
        // Assignments data parsing
        List<List<int>> assignments = ParseListOfListOfInts(assignmentsData);
        PrintListOfListOfInt(assignments);
        */
        // Trajectories files data parsing
        String[] fileNames = ParseStringArray(filesData);
        PrintStringArray(fileNames);
    }
}

