using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;


public class PersonMovement : MonoBehaviour
{
    GameObject t1;
    GameObject t2;
    GameObject t3;
    GameObject RectArea;
    GameObject TriArea;
    SpriteRenderer Render;
    GameObject door;
    GameObject house;
    Vector3 origin;

    // Start is called before the first frame update
    private UnityEngine.Color insideColor;
    private UnityEngine.Color outsideColor;
    private SpriteRenderer triAreaRenderer;
    private SpriteRenderer rectAreaRenderer;

    private const string BASE_URL = "https://connect.getseam.com";
    private string apiKey;
    private string deviceId;
    private bool? lockStatus = null; // null = unknown, true = locked, false = unlocked
    private bool isCheckingLock = false;

    private Renderer houseRenderer;
    private UnityEngine.Color originalColor;
    private bool isTransparent = false;


    void Start()
    {
        apiKey = System.Environment.GetEnvironmentVariable("SEAM_API_KEY");
        if (!string.IsNullOrEmpty(apiKey))
        {
            StartCoroutine(InitializeSeam());
        }

        // Cache GameObjects and Components
        t1 = GameObject.Find("Point1");
        t2 = GameObject.Find("Point2");
        t3 = GameObject.Find("Point3");
        RectArea = GameObject.Find("Rectangle");
        TriArea = GameObject.Find("Triangle");
        door = GameObject.Find("01_low");
        house = GameObject.Find("House");
        origin = door.transform.eulerAngles;
        houseRenderer = house.GetComponent<Renderer>();
        originalColor = houseRenderer.material.color;

        // Cache Renderers
        triAreaRenderer = TriArea.GetComponent<SpriteRenderer>();
        rectAreaRenderer = RectArea.GetComponent<SpriteRenderer>();

        // Parse colors once
        UnityEngine.ColorUtility.TryParseHtmlString("#86FF89", out insideColor);
        UnityEngine.ColorUtility.TryParseHtmlString("#FF8686", out outsideColor);
    }

    void Update()
    {
        if (Time.frameCount % 60 == 0)
        {
            bool isInTriangle = PointInTri(t1.transform.position, t2.transform.position, t3.transform.position, transform.position);
            bool isInRectangle = RectArea.GetComponent<BoxCollider>().bounds.Contains(transform.position);

            // 房屋透明控制
            if (isInTriangle || isInRectangle)
            {
                if (!isTransparent) MakeTransparent();
            }
            else
            {
                if (isTransparent & (bool)lockStatus) MakeSolid();
            }

            // ... 你原有的三角区、矩形区处理
            if (isInTriangle)
            {
                triAreaRenderer.color = insideColor;
                door.transform.eulerAngles = new Vector3(-90, 0, 0);
                if (lockStatus == true && !isCheckingLock)
                {
                    StartCoroutine(UnlockLock());
                }
            }
            else
            {
                triAreaRenderer.color = outsideColor;
                if (!isInTriangle && !isInRectangle)
                {
                    StartCoroutine(DelayedLockCheck());
                }
            }

            if (isInRectangle)
            {
                rectAreaRenderer.color = insideColor;
            }
            else
            {
                rectAreaRenderer.color = outsideColor;
            }
        }

    }


    public void move(float newX, float newY, float newZ)
    {
        transform.position = new Vector3(newX, newY, newZ);
    }

    /* Returns true if point p lies inside triangle a-b-c */
    Boolean PointInTri(Vector3 t1, Vector3 t2, Vector3 t3, Vector3 pos)
    {
        // create temporary points to convert from V3 to V2 by replacing y with z
        Vector3 temp1 = t1;
        Vector3 temp2 = t2;
        Vector3 temp3 = t3;
        Vector3 temp_pos = pos;

        temp1.y = temp1.z;
        temp2.y = temp2.z;
        temp3.y = temp3.z;
        temp_pos.y = temp_pos.z;

        Vector2 a = temp1;
        Vector2 b = temp2;
        Vector2 c = temp3;
        Vector2 p = temp_pos;

        // calculate if inside
        Vector2 v0 = b - c;
        Vector2 v1 = a - c;
        Vector2 v2 = p - c;
        float dot00 = Vector2.Dot(v0, v0);
        float dot01 = Vector2.Dot(v0, v1);
        float dot02 = Vector2.Dot(v0, v2);
        float dot11 = Vector2.Dot(v1, v1);
        float dot12 = Vector2.Dot(v1, v2);
        float invDenom = 1.0f / (dot00 * dot11 - dot01 * dot01);
        float u = (dot11 * dot02 - dot01 * dot12) * invDenom;
        float v = (dot00 * dot12 - dot01 * dot02) * invDenom;
        return (u > 0.0f) && (v > 0.0f) && (u + v < 1.0f);
    }

    IEnumerator InitializeSeam()
    {
        UnityWebRequest request = UnityWebRequest.Get(BASE_URL + "/locks/list");
        request.SetRequestHeader("Authorization", "Bearer " + apiKey);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Failed to fetch locks: " + request.error);
            yield break;
        }

        var json = request.downloadHandler.text;
        var parsed = JObject.Parse(json);
        var locks = parsed["locks"] as JArray;

        if (locks == null || locks.Count == 0)
        {
            Debug.LogError("No locks found.");
            yield break;
        }

        foreach (var lockObj in locks)
        {
            string name = lockObj["display_name"]?.ToString();
            if (name == "Front Door")
            {
                deviceId = lockObj["device_id"]?.ToString();
                lockStatus = lockObj["properties"]?["locked"]?.ToObject<bool>();
                Debug.Log($"Found lock: {name}, ID: {deviceId}, Locked: {lockStatus}");
                yield break;
            }
        }

        Debug.LogError("Lock with name 'Front Door' not found.");
    }


    IEnumerator UnlockLock()
    {
        isCheckingLock = true;
        string body = $"{{\"device_id\":\"{deviceId}\"}}";
        UnityWebRequest req = new UnityWebRequest(BASE_URL + "/locks/unlock_door", "POST");
        byte[] raw = Encoding.UTF8.GetBytes(body);
        req.uploadHandler = new UploadHandlerRaw(raw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Authorization", "Bearer " + apiKey);
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();

        var parsed = JObject.Parse(req.downloadHandler.text);
        string actionId = parsed["action_attempt"]?["action_attempt_id"]?.ToString();

        if (!string.IsNullOrEmpty(actionId))
            yield return StartCoroutine(PollActionStatus(actionId, false)); // false = now unlocked
    }

    IEnumerator LockLock()
    {
        isCheckingLock = true;
        string body = $"{{\"device_id\":\"{deviceId}\"}}";
        UnityWebRequest req = new UnityWebRequest(BASE_URL + "/locks/lock_door", "POST");
        byte[] raw = Encoding.UTF8.GetBytes(body);
        req.uploadHandler = new UploadHandlerRaw(raw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Authorization", "Bearer " + apiKey);
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();

        var parsed = JObject.Parse(req.downloadHandler.text);
        string actionId = parsed["action_attempt"]?["action_attempt_id"]?.ToString();

        if (!string.IsNullOrEmpty(actionId))
            yield return StartCoroutine(PollActionStatus(actionId, true)); // true = now locked
    }

    IEnumerator PollActionStatus(string actionAttemptId, bool finalLockState)
    {
        string url = $"{BASE_URL}/action_attempts/get?action_attempt_id={actionAttemptId}";

        while (true)
        {
            UnityWebRequest req = UnityWebRequest.Get(url);
            req.SetRequestHeader("Authorization", "Bearer " + apiKey);
            yield return req.SendWebRequest();

            string json = req.downloadHandler.text;
            string status = JObject.Parse(json)?["action_attempt"]?["status"]?.ToString();

            if (status == "success" || status == "error")
            {
                Debug.Log($"Seam Lock State Update: {status}");
                if (status == "success")
                    lockStatus = finalLockState;
                break;
            }

            yield return new WaitForSeconds(1f);
        }

        isCheckingLock = false;
    }

    IEnumerator DelayedLockCheck()
    {
        yield return new WaitForSeconds(1.5f); // Wait 2 seconds

        // Re-check in case position changed during wait
        bool stillOutside = !PointInTri(t1.transform.position, t2.transform.position, t3.transform.position, transform.position) &&
                            !RectArea.GetComponent<BoxCollider>().bounds.Contains(transform.position);

        if (stillOutside)
        {
            door.transform.eulerAngles = origin;

            if (lockStatus == false && !isCheckingLock)
            {
                StartCoroutine(LockLock());
            }
        }
    }

    // 设为透明
    void MakeTransparent()
    {
        var mat = houseRenderer.material;
        mat.SetFloat("_Mode", 3);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        UnityEngine.Color c = mat.color;
        c.a = 0.3f;
        mat.color = c;
        isTransparent = true;
    }

    // 设为实心
    void MakeSolid()
    {
        var mat = houseRenderer.material;
        mat.SetFloat("_Mode", 0);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
        mat.SetInt("_ZWrite", 1);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = -1;
        UnityEngine.Color c = originalColor;
        c.a = 1.0f;
        mat.color = c;
        isTransparent = false;
    }

}
