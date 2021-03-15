using System;
using System.Net;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;

using UnityEngine;
using UnityEngine.UI;



public class PythonServer : MonoBehaviour {
    volatile bool keepReading = false;
    private bool isAtStartup = true;

    public int port;
    public String[] fileNames;
    private String dataDir = "";

    System.Threading.Thread socketThread;
    Socket listener;
    Socket handler;
    String data = null;
    
    void Start(){
        Application.runInBackground = true;
        dataDir += Application.dataPath + "/Datasets/";
    }

    void Update(){
        if(isAtStartup){
            if(Input.GetKeyDown(KeyCode.Space)){
                isAtStartup = false;
                StartServer();
            }
        }
    }

    void OnGUI(){
        if(isAtStartup)
            GUI.Label(new Rect(2, 10, 150, 100), "Press Space to get data");
        else {
            GUI.Label(new Rect(2, 10, 150, 100), "Acquiring data...");
        }
        String str = "Data : ";
        if(data != null)
            str += data.ToString();
        GUI.Label(new Rect(2, 30, 1500, 100), str);
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
        data = null;

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

                    if(bytesRec <= 0){
                        keepReading = false;
                        handler.Disconnect(true);
                        break;
                    }
                    data += Encoding.UTF8.GetString(bytes, 0, bytesRec);
                    Debug.Log(data);
                    if(data.IndexOf("<EOF>") > -1)
                        break;

                    bytesToSend = Encoding.UTF8.GetBytes("Unity : data received");
                    handler.Send(bytesToSend);
                    Debug.Log("Message sent back to Python");
                    break;
                    //System.Threading.Thread.Sleep(1);
                }
                //System.Threading.Thread.Sleep(1);
                break;
            }
        } catch(Exception e){
            Debug.Log("ERROR WHILE GETING DATA : " + e.ToString());
        }
                        
        if(handler != null && handler.Connected){
            handler.Disconnect(false);
            listener.Close();
            Debug.Log("Disconnected");
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
    }

    void OnDisable(){
        StopServer();
    }
}

