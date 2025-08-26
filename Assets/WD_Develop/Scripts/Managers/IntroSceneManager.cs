using System;
using UnityEngine;
using UnityEngine.UI;

public class IntroSceneManager : MonoBehaviour
{
   [SerializeField] private Button startBtn;

   private void Start()
   {
      SoundManager.Instance.PlaySound(AudioMixerType.BGM,"TitleBgm",true);
      
      startBtn.onClick.AddListener(func_startbtn);
   }

   private void func_startbtn()
   {
      SceneLoader.Instance.LoadScene("LobbyTest");
   }
}
