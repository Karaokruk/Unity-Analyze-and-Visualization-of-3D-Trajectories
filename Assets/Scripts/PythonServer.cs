using System;
using System.Net;
//using System.Collections;
//using System.IO;
using System.Net.Sockets;
using System.Text;
using UnityEngine;



public class PythonServer : MonoBehaviour {
    System.Threading.Thread socketThread;
    volatile bool keepReading = false;
    private bool isAtStartup = true;

    Socket listener;
    Socket handler;
    String data = null;
    
    void Start(){
        Application.runInBackground = true;
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
            String str = "Data : ";
            if(data != null)
                str += data.ToString();
            GUI.Label(new Rect(2, 30, 1500, 100), str);
        }
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

        Debug.Log("Ip " + GetIpAddress().ToString());
        IPAddress[] ipArray = Dns.GetHostAddresses(GetIpAddress());
        IPEndPoint localEndPoint = new IPEndPoint(ipArray[0], 5000);

        listener = new Socket(ipArray[0].AddressFamily, SocketType.Stream, ProtocolType.Tcp);

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
                    int bytesRec = handler.Receive(bytes);
                    Debug.Log("Received from Server");

                    if(bytesRec <= 0){
                        keepReading = false;
                        handler.Disconnect(true);
                        break;
                    }
                    data += Encoding.UTF8.GetString(bytes, 0, bytesRec);
                    Debug.Log(data);
                    if(data.IndexOf("<EOF>") > -1)
                        break;

                    Byte[] bytesToSend = Encoding.UTF8.GetBytes("Message sent to Unity");
                    //try{
                    handler.Send(bytesToSend);
                    Debug.Log("Message sent back to Python");
                    //} catch (SocketException e){
                    //    Debug.Log("ERROR WHILE SENDING BACK A MESSAGE : " + e);
                    //    break;
                    //}

                    System.Threading.Thread.Sleep(1);
                }
                System.Threading.Thread.Sleep(1);
            }
        } catch(Exception e){
            Debug.Log("ERROR WHILE GETING DATA : " + e.ToString());
        }
        Debug.Log("Finishing Listening");
        StopServer();
        isAtStartup = true;
    }

    void StopServer(){
        keepReading = false;

        if(socketThread != null)
            socketThread.Abort();

        if(handler != null && handler.Connected){
            handler.Disconnect(false);
            Debug.Log("Disconnected");
        }
    }

    void OnDisable(){
        StopServer();
    }
}

