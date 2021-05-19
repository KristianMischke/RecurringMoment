using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimationAudioTrigger : StateMachineBehaviour
{

    [SerializeField] private AudioClip _entranceClip;
    [SerializeField] private float _entranceVolume;
    [SerializeField] private AudioClip _exitClip;
    [SerializeField] private float _exitVolume;

    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
	if(_entranceClip != null)
            AudioSource.PlayClipAtPoint(_entranceClip, Camera.main.transform.position, _entranceVolume);
    }

    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        
    }

    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
	if(_exitClip != null)
            AudioSource.PlayClipAtPoint(_exitClip, Camera.main.transform.position, _exitVolume);
    }

    override public void OnStateMove(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // Implement code that processes and affects root motion
    }

    override public void OnStateIK(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // Implement code that sets up animation IK (inverse kinematics)
    }
}
