using System;
using UnityEngine;



public class Sandbox : MonoBehaviour
{
    float Babylonian(float num)
    {
        float value1 = num;
        if (value1 < 0)
        {
            return -1;
        }

        float oValue = 1;
        float value2 = value1;
        float e = 0.0001f;

        while (value2 - oValue > e)
        {
            value2 = (value2 + oValue) / 2;
            oValue = value1 / value2;
        }

        return oValue;
    }
    private void Start()
    {
        
    }
}
