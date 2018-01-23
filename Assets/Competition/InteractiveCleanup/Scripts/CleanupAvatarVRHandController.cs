using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;

namespace SIGVerse.Competition.InteractiveCleanup
{
	public class CleanupAvatarVRHandController : MonoBehaviour
	{
#if SIGVERSE_USING_OCULUS_RIFT

		public enum HandType
		{
			LeftHand,
			RightHand,
		}

		public HandType  handType;
		public Transform hand;

//		public float handTrigger1D =1;
		//-----------
		private Transform thumb1, index1, middle1, ring1, pinky1;
		private Transform thumb2, index2, middle2, ring2, pinky2;
		private Transform thumb3, index3, middle3, ring3, pinky3;

//		private Quaternion handStart   , handEnd;
		private Quaternion thumb1Start , thumb1End , thumb2Start , thumb2End , thumb3Start , thumb3End;
		private Quaternion index1Start , index1End , index2Start , index2End , index3Start , index3End;
		private Quaternion middle1Start, middle1End, middle2Start, middle2End, middle3Start, middle3End;
		private Quaternion ring1Start  , ring1End  , ring2Start  , ring2End  , ring3Start  , ring3End;
		private Quaternion pinky1Start , pinky1End , pinky2Start , pinky2End , pinky3Start , pinky3End;


		void Awake()
		{
			string typeStr = (this.handType == HandType.LeftHand)? "Left" : "Right";

			this.thumb1  = this.hand.Find("Ethan"+typeStr+"HandThumb1");
			this.thumb2  = this.hand.Find("Ethan"+typeStr+"HandThumb1/Ethan"+typeStr+"HandThumb2");  
			this.thumb3  = this.hand.Find("Ethan"+typeStr+"HandThumb1/Ethan"+typeStr+"HandThumb2/Ethan"+typeStr+"HandThumb3");

			this.index1  = this.hand.Find("Ethan"+typeStr+"HandIndex1");
			this.index2  = this.hand.Find("Ethan"+typeStr+"HandIndex1/Ethan"+typeStr+"HandIndex2");  
			this.index3  = this.hand.Find("Ethan"+typeStr+"HandIndex1/Ethan"+typeStr+"HandIndex2/Ethan"+typeStr+"HandIndex3");

			this.middle1 = this.hand.Find("Ethan"+typeStr+"HandMiddle1");
			this.middle2 = this.hand.Find("Ethan"+typeStr+"HandMiddle1/Ethan"+typeStr+"HandMiddle2");  
			this.middle3 = this.hand.Find("Ethan"+typeStr+"HandMiddle1/Ethan"+typeStr+"HandMiddle2/Ethan"+typeStr+"HandMiddle3");

			this.ring1   = this.hand.Find("Ethan"+typeStr+"HandRing1");
			this.ring2   = this.hand.Find("Ethan"+typeStr+"HandRing1/Ethan"+typeStr+"HandRing2");  
			this.ring3   = this.hand.Find("Ethan"+typeStr+"HandRing1/Ethan"+typeStr+"HandRing2/Ethan"+typeStr+"HandRing3");

			this.pinky1  = this.hand.Find("Ethan"+typeStr+"HandPinky1");
			this.pinky2  = this.hand.Find("Ethan"+typeStr+"HandPinky1/Ethan"+typeStr+"HandPinky2");  
			this.pinky3  = this.hand.Find("Ethan"+typeStr+"HandPinky1/Ethan"+typeStr+"HandPinky2/Ethan"+typeStr+"HandPinky3");  
		}


		// Use this for initialization
		void Start()
		{
//			this.handStart    = this.hand   .localRotation;
			this.thumb1Start  = this.thumb1 .localRotation;   this.thumb2Start  = this.thumb2 .localRotation;   this.thumb3Start  = this.thumb3 .localRotation;
			this.index1Start  = this.index1 .localRotation;   this.index2Start  = this.index2 .localRotation;   this.index3Start  = this.index3 .localRotation;
			this.middle1Start = this.middle1.localRotation;   this.middle2Start = this.middle2.localRotation;   this.middle3Start = this.middle3.localRotation;
			this.ring1Start   = this.ring1  .localRotation;   this.ring2Start   = this.ring2  .localRotation;   this.ring3Start   = this.ring3  .localRotation;
			this.pinky1Start  = this.pinky1 .localRotation;   this.pinky2Start  = this.pinky2 .localRotation;   this.pinky3Start  = this.pinky3 .localRotation;

			float xySign = (this.handType == HandType.LeftHand)? 1.0f : -1.0f;

//			this.handEnd = Quaternion.Euler(xySign*(-97), xySign*(-10), 0);

			this.thumb1End  = Quaternion.Euler(xySign*(+63), xySign*(-102), -53);
			this.thumb2End  = Quaternion.Euler(xySign*(- 6), xySign*(- 24), +24);
			this.thumb3End  = Quaternion.Euler(xySign*(  1), xySign*(-  3), +14);

			this.index1End  = Quaternion.Euler(xySign*(-1), xySign*(+10), -16);
			this.index2End  = Quaternion.Euler(xySign*( 0), xySign*(  0), -30);
			this.index3End  = Quaternion.Euler(xySign*(+2), xySign*(+ 5), -30);

			this.middle1End = Quaternion.Euler(xySign*(+ 2), xySign*(+12), +60);
			this.middle2End = Quaternion.Euler(xySign*(+34), xySign*(-34), +26);
			this.middle3End = Quaternion.Euler(xySign*(  0), xySign*(  0), +19);

			this.ring1End   = Quaternion.Euler(xySign*(+33), xySign*(- 2), +29);
			this.ring2End   = Quaternion.Euler(xySign*(+25), xySign*(+13), +29);
			this.ring3End   = Quaternion.Euler(xySign*(+17), xySign*(+ 5), +14);

			this.pinky1End  = Quaternion.Euler(xySign*(+34), xySign*(+17), + 4);
			this.pinky2End  = Quaternion.Euler(xySign*(+47), xySign*(+30), +19);
			this.pinky3End  = Quaternion.Euler(xySign*(-20), xySign*(+41), +12);
		}

		// Update is called once per frame
		void LateUpdate()
		{
			float handTrigger1D = (this.handType == HandType.LeftHand)? OVRInput.Get(OVRInput.RawAxis1D.LHandTrigger) : OVRInput.Get(OVRInput.RawAxis1D.RHandTrigger);

			// Change hand posture
//			this.hand   .localRotation = Quaternion.Slerp(this.handStart   , this.handEnd   , handTrigger1D);

			this.thumb1 .localRotation = Quaternion.Slerp(this.thumb1Start , this.thumb1End , handTrigger1D);
			this.thumb2 .localRotation = Quaternion.Slerp(this.thumb2Start , this.thumb2End , handTrigger1D);
			this.thumb3 .localRotation = Quaternion.Slerp(this.thumb3Start , this.thumb3End , handTrigger1D);

			this.index1 .localRotation = Quaternion.Slerp(this.index1Start , this.index1End , handTrigger1D);
			this.index2 .localRotation = Quaternion.Slerp(this.index2Start , this.index2End , handTrigger1D);
			this.index3 .localRotation = Quaternion.Slerp(this.index3Start , this.index3End , handTrigger1D);

			this.middle1.localRotation = Quaternion.Slerp(this.middle1Start, this.middle1End, handTrigger1D);
			this.middle2.localRotation = Quaternion.Slerp(this.middle2Start, this.middle2End, handTrigger1D);
			this.middle3.localRotation = Quaternion.Slerp(this.middle3Start, this.middle3End, handTrigger1D);

			this.ring1  .localRotation = Quaternion.Slerp(this.ring1Start  , this.ring1End  , handTrigger1D);
			this.ring2  .localRotation = Quaternion.Slerp(this.ring2Start  , this.ring2End  , handTrigger1D);
			this.ring3  .localRotation = Quaternion.Slerp(this.ring3Start  , this.ring3End  , handTrigger1D);

			this.pinky1 .localRotation = Quaternion.Slerp(this.pinky1Start , this.pinky1End , handTrigger1D);
			this.pinky2 .localRotation = Quaternion.Slerp(this.pinky2Start , this.pinky2End , handTrigger1D);
			this.pinky3 .localRotation = Quaternion.Slerp(this.pinky3Start , this.pinky3End , handTrigger1D);
		}
#endif
	}
}

