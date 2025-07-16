using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;
using System.Text;
using UnityEngine.Networking;

public class SerialPortReader : MonoBehaviour
{
    static string[] portNames = SerialPort.GetPortNames();
    static string port_name = "\\\\.\\" + portNames[0];
    SerialPort serial = new SerialPort(port_name, 115200);
    private int num_bytes = 32;
    TMP_Dropdown dropdown;
    //public byte[] data; 
    // Start is called before the first frame update
    private Queue<float> rssiHistory = new Queue<float>();
    private int rssiHistoryLength = 10; // Adjust this value based on desired responsiveness vs. smoothness


//This section declares the objects in the GUI which are going to be constantly updated throughout the runtime.

    TMP_Text d1;
    TMP_Text d2;
    TMP_Text d3;
    TMP_Text d4;
    TMP_Text d5;
    TMP_Text d6;

    private GameObject person;
    private Animator animator;

    byte[] data;

    public struct outData
    {
        //'<BH2fB'
        // little endian
        // unsigned char
        // unsigned short
        // 2?
        // float
        // unsigned char
        public byte start;
        public ushort distance;
        public float angle;
        public float rssiDiff;
        public byte end;
    }

    void Start()
    {

        // Connecting the object created in Unity with the variables declared above.
        dropdown = GameObject.Find("SerialPortDropdown").GetComponent<TMP_Dropdown>();
        serial.ReadTimeout = 1000;

        d1 = GameObject.Find("d1").GetComponent<TMP_Text>();
        d2 = GameObject.Find("d2").GetComponent<TMP_Text>();
        d3 = GameObject.Find("d3").GetComponent<TMP_Text>();
        d4 = GameObject.Find("d4").GetComponent<TMP_Text>();
        d5 = GameObject.Find("d5").GetComponent<TMP_Text>();
        d6 = GameObject.Find("d6").GetComponent<TMP_Text>();
        person = GameObject.Find("Person");
        animator = person.GetComponent<Animator>();

        //animator.applyRootMotion = false;

    }

    // Update is called once per frame
    void Update()
    {
        // Update the serial port dropdown list only if necessary.
        if (Time.frameCount % 60 == 0)
        {
            List<String> ports = new List<String>();
            ports = SerialPort.GetPortNames().ToList();
            dropdown.ClearOptions();
            dropdown.AddOptions(ports);
        }
    }

    // Called whenever the Open Port button is pressed. Starts the demonstration
    public void openPort(string test)
    {
        string port = "\\\\.\\";
        port += dropdown.options[dropdown.value].text;
        print("Opening port: " + port);
        serial.Open();
        StartCoroutine(ReadSerialWhenReady());
    }

    // Called whenever the Close Port button is pressed. Ends the demonstration (but not the program)
    public void closePort(string test)
    {
        StopCoroutine(ReadSerialWhenReady());
        serial.Close();
    }

    // A function to smooth the value of RssiDiff read from the port
    private float UpdateRssi(float newRssiDiff, double y)
    {   if (newRssiDiff < 0)
        {
            return newRssiDiff;
        }
        // Parameters for the sensitivity adjustment
        double alpha = 5.0; // Controls the steepness of the transition
        double y0 = 5.0;     // The distance at which the sensitivity starts to change

        // Compute the weight based on the distance (y)
        double weight = ComputeWeight(y, alpha, y0);

        // Calculate the smoothed RSSI
        float smoothedRssi = ComputeSmoothedRssi(newRssiDiff, weight);

        return smoothedRssi;
    }

    private double ComputeWeight(double y, double alpha, double y0)
    {
        // Use a sigmoid function to compute the weight based on distance
        return 1.0 / (1.0 + Math.Exp(-alpha * (y - y0)));
    }

    private float ComputeSmoothedRssi(float newRssiDiff, double weight)
    {

        // Maintain the history length
        if (rssiHistory.Count >= rssiHistoryLength)
        {
            rssiHistory.Dequeue(); // Remove the oldest entry
        }
        rssiHistory.Enqueue(newRssiDiff);

        // Calculate the average RSSI from the history
        float averageRssi = rssiHistory.Average();

        // Adjust the smoothed RSSI based on the weight
        return (float)(weight * newRssiDiff + (1.0 - weight) * averageRssi);
    }

    // Update the data table using functions defined next
    void ProcessData(byte[] data)
    {
        outData dataResult = getStructData(data); // Assume this method parses the byte array and returns an outData object
        double x = (Math.Sin(Math.PI * dataResult.angle / 180) * dataResult.distance / 100);
        double y = (Math.Cos(Math.PI * dataResult.angle / 180) * dataResult.distance / 100);

        //float smoothedRssi = UpdateRssi(dataResult.rssiDiff, y);
        

        // Use smoothedRssi instead of dataResult.rssiDiff for logic
        if (dataResult.rssiDiff < 0.0)
        {
            move(person, -1f * (float)x, person.transform.position.y*2, (float)y);
            displayDataPoint("1", (100 * x).ToString(), (-100 * y).ToString(), dataResult.distance.ToString(), dataResult.angle.ToString(), dataResult.rssiDiff.ToString());
        }
        else
        {
            move(person, -1f * (float)x, person.transform.position.y*2, -1f * (float)y);
            displayDataPoint("1", (100 * x).ToString(), (100 * y).ToString(), dataResult.distance.ToString(), dataResult.angle.ToString(), dataResult.rssiDiff.ToString());
        }
    }

    //Parsing the data received through the port using prefound data structure. Some conditions remains unclear, 
    // but the programming is working so keep untouched as possible.
    outData getStructData(byte[] data)
    {
        int offset = 0;
        outData result = new outData();
        int outDate_length = System.Runtime.InteropServices.Marshal.SizeOf(result);

        while (offset < data.Length)
        {
            outData temp = new outData();

            if (data[offset] == 0xAA && offset + outDate_length <= data.Length)
            {
                MemoryStream buffer = new MemoryStream(data, offset, data.Length - offset);
                using (var br = new BinaryReader(buffer))
                {
                    try
                    {
                        temp.start = br.ReadByte();
                        temp.distance = br.ReadUInt16();
                        temp.angle = br.ReadSingle();
                        temp.rssiDiff = br.ReadSingle();
                        temp.end = br.ReadByte();

                        if (temp.end == 0x55)
                        {
                            result = temp;
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.Log("Error processing data: " + ex.Message);
                        return default(outData);
                    }
                }
            }
            offset++;
        }

        result.angle = MathF.Round(result.angle, 2);
        result.rssiDiff = MathF.Round(result.rssiDiff, 2);
        return result;
    }

    void displayDataPoint(string tag, string x, string y, string dist, string angle, string rssiDiff)
    {
        d1.text = tag;
        d2.text = x;
        d3.text = y;
        d4.text = dist;
        d5.text = angle;
        d6.text = rssiDiff;
    }

    public void move(GameObject obj, float newX, float newY, float newZ)
    {
        Vector3 currentPos = obj.transform.position;
        Vector3 targetPos = new Vector3(newX, newY, newZ);
        float distance = Vector3.Distance(currentPos, targetPos);

        // Assume a 0.5 second move duration (same as SmoothMove duration)
        float speed = distance / 0.5f;

        if (animator != null)
            animator.SetFloat("Speed", speed);

        StartCoroutine(SmoothMove(obj, targetPos, 0.5f)); // Move over 0.5 seconds
        
    }

    //Coroutine function that smooths the 'Person' object's movement, larger value for duration increase the runtime of movement animation,
    // a duration value too small can make the movement seems jarring.
    IEnumerator SmoothMove(GameObject obj, Vector3 target, float duration)
    {
        Vector3 start = obj.transform.position;
        float time = 0;
        while (time < duration)
        {
            float t = time / duration;
            obj.transform.position = Vector3.Lerp(start, target, t);
            time += Time.deltaTime;
            yield return null;
        }

        obj.transform.position = target;  // Ensure target position is reached.
    }

    //Coroutine function that ensures the reading and processing of data can happen seperately and do not interrupt each other
    IEnumerator ReadSerialWhenReady()
    {
        UnityEngine.Debug.Log("Port opened successfully.");
        byte[] buffer = new byte[1024]; // Larger buffer
        int bufferIndex = 0;

        while (true)
        {
            try
            {
                while (serial.BytesToRead > 0)
                {

                    int bytesRead = serial.Read(buffer, bufferIndex, buffer.Length - bufferIndex);
                    bufferIndex += bytesRead;

                    if (bufferIndex >= num_bytes) // Enough data to process
                    {
                        ProcessData(new ArraySegment<byte>(buffer, 0, num_bytes).ToArray());
                        Array.Copy(buffer, num_bytes, buffer, 0, bufferIndex - num_bytes); // Shift remaining data
                        bufferIndex -= num_bytes;
                    }
                }
            }

            catch (TimeoutException)
            {
                UnityEngine.Debug.LogError("Read timeout occurred on serial port.");
            }
            yield return null;
        }
    }

}