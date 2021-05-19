using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AudioSlider : MonoBehaviour
{

    [SerializeField] private Slider _slider;

    private void Start()
    {
        _slider.value = AudioListener.volume;
    }

    void Update()
    {
        AudioListener.volume = _slider.value;
    }
}
