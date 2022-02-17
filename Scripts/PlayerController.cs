using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour
{
    private Rigidbody rb;
    public float speed;


    // Use this for initialization
    void Start()
    {
        rb = GetComponent<Rigidbody>();


    }

    void FixedUpdate()
    {
        float moveHorizontal = Input.GetAxis("Horizontal");
        float moveVertical = Input.GetAxis("Vertical");
        Vector3 movement = new Vector3(moveHorizontal, 0.0f, moveVertical);
        rb.AddForce(movement * speed);
        if (Input.GetKeyDown("space"))
        {
            rb.AddForce(Vector3.up * 5, ForceMode.Impulse);

        }
    }
    void OnTriggerEnter(Collider other)
    {

        //Destroy(other.gameObject);
        if (other.gameObject.CompareTag("Pick Up"))
        {
            other.gameObject.SetActive(false);

        }
    }
}
