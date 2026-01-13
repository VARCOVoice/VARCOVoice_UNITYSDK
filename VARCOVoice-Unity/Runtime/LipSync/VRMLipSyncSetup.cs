using UnityEngine;
using VARCOVoice.LipSync;

namespace VARCOVoice
{
    /// <summary>
    /// VRM/VRoid 모델용 립싱크 프로파일 생성 유틸리티
    /// </summary>
    public static class VRMLipSyncSetup
    {
        /// <summary>
        /// VRM 표준 블렌드셰이프 이름
        /// VRoid Studio에서 생성된 모델과 호환
        /// </summary>
        public static readonly string[] VRMBlendShapeNames = new[]
        {
            "A",    // ㅏ - 입 크게
            "I",    // ㅣ - 입 옆으로
            "U",    // ㅜ - 입 오므림
            "E",    // ㅔ - 입 중간
            "O",    // ㅗ - 입 동그랗게
        };
        
        /// <summary>
        /// VRM 모델용 LipSyncProfile 생성
        /// </summary>
        public static LipSyncProfile CreateVRMProfile()
        {
            var profile = ScriptableObject.CreateInstance<LipSyncProfile>();
            
            profile.BlendShapes.Clear();
            
            // VRM은 5개 모음만 사용 (AIUEO)
            // 우리의 15개 Viseme를 5개로 매핑
            
            // Silence - 입 다물기 (블렌드셰이프 없음)
            profile.BlendShapes.Add(new VisemeBlendShape 
            { 
                Viseme = VisemeType.Silence, 
                BlendShapeName = null,
                Weight = 0f 
            });
            
            // AA (ㅏ, ㅓ) → VRM "A"
            profile.BlendShapes.Add(new VisemeBlendShape 
            { 
                Viseme = VisemeType.AA, 
                BlendShapeName = "Fcl_MTH_A",
                Weight = 100f 
            });
            
            // EE (ㅣ, ㅔ) → VRM "I"
            profile.BlendShapes.Add(new VisemeBlendShape 
            { 
                Viseme = VisemeType.EE, 
                BlendShapeName = "Fcl_MTH_I",
                Weight = 100f 
            });
            
            // IH → VRM "I"
            profile.BlendShapes.Add(new VisemeBlendShape 
            { 
                Viseme = VisemeType.IH, 
                BlendShapeName = "Fcl_MTH_I",
                Weight = 80f 
            });
            
            // OH (ㅗ) → VRM "O"
            profile.BlendShapes.Add(new VisemeBlendShape 
            { 
                Viseme = VisemeType.OH, 
                BlendShapeName = "Fcl_MTH_O",
                Weight = 100f 
            });
            
            // OO (ㅜ) → VRM "U"
            profile.BlendShapes.Add(new VisemeBlendShape 
            { 
                Viseme = VisemeType.OO, 
                BlendShapeName = "Fcl_MTH_U",
                Weight = 100f 
            });
            
            // CH, SS, TH → VRM "I" (치찰음)
            profile.BlendShapes.Add(new VisemeBlendShape 
            { 
                Viseme = VisemeType.CH, 
                BlendShapeName = "Fcl_MTH_I",
                Weight = 50f 
            });
            
            profile.BlendShapes.Add(new VisemeBlendShape 
            { 
                Viseme = VisemeType.SS, 
                BlendShapeName = "Fcl_MTH_I",
                Weight = 40f 
            });
            
            profile.BlendShapes.Add(new VisemeBlendShape 
            { 
                Viseme = VisemeType.TH, 
                BlendShapeName = "Fcl_MTH_I",
                Weight = 60f 
            });
            
            // FF → VRM "U" (입술 앞으로)
            profile.BlendShapes.Add(new VisemeBlendShape 
            { 
                Viseme = VisemeType.FF, 
                BlendShapeName = "Fcl_MTH_U",
                Weight = 70f 
            });
            
            // PP, KK (입술 닫힘) → 없음 또는 약한 A
            profile.BlendShapes.Add(new VisemeBlendShape 
            { 
                Viseme = VisemeType.PP, 
                BlendShapeName = null,
                Weight = 0f 
            });
            
            profile.BlendShapes.Add(new VisemeBlendShape 
            { 
                Viseme = VisemeType.KK, 
                BlendShapeName = "Fcl_MTH_A",
                Weight = 30f 
            });
            
            // NN, RR, DD → VRM "E"
            profile.BlendShapes.Add(new VisemeBlendShape 
            { 
                Viseme = VisemeType.NN, 
                BlendShapeName = "Fcl_MTH_E",
                Weight = 50f 
            });
            
            profile.BlendShapes.Add(new VisemeBlendShape 
            { 
                Viseme = VisemeType.RR, 
                BlendShapeName = "Fcl_MTH_E",
                Weight = 60f 
            });
            
            profile.BlendShapes.Add(new VisemeBlendShape 
            { 
                Viseme = VisemeType.DD, 
                BlendShapeName = "Fcl_MTH_E",
                Weight = 50f 
            });
            
            // Settings
            profile.Smoothing = 0.2f;  // VRM은 약간 더 빠른 반응
            profile.Intensity = 1.2f;  // 약간 강조
            
            return profile;
        }
        
        /// <summary>
        /// VRM 모델에서 얼굴 메시 자동 찾기
        /// </summary>
        public static SkinnedMeshRenderer FindFaceMesh(GameObject vrmRoot)
        {
            // VRM 표준 이름으로 찾기
            string[] possibleNames = { "Face", "Body", "face", "body", "Mesh" };
            
            var renderers = vrmRoot.GetComponentsInChildren<SkinnedMeshRenderer>();
            
            foreach (var renderer in renderers)
            {
                // 블렌드셰이프가 있는 메시 찾기
                if (renderer.sharedMesh != null && renderer.sharedMesh.blendShapeCount > 0)
                {
                    // AIUEO 블렌드셰이프가 있는지 확인
                    int aIndex = renderer.sharedMesh.GetBlendShapeIndex("Fcl_MTH_A");
                    if (aIndex >= 0)
                    {
                        return renderer;
                    }
                }
            }
            
            // 못 찾으면 블렌드셰이프가 가장 많은 메시 반환
            SkinnedMeshRenderer best = null;
            int maxBlendShapes = 0;
            
            foreach (var renderer in renderers)
            {
                if (renderer.sharedMesh != null && 
                    renderer.sharedMesh.blendShapeCount > maxBlendShapes)
                {
                    maxBlendShapes = renderer.sharedMesh.blendShapeCount;
                    best = renderer;
                }
            }
            
            return best;
        }
        
        /// <summary>
        /// VRM 모델에 립싱크 설정 자동 적용
        /// </summary>
        public static LipSyncPlayer SetupVRMLipSync(GameObject vrmRoot)
        {
            // 얼굴 메시 찾기
            var faceMesh = FindFaceMesh(vrmRoot);
            if (faceMesh == null)
            {
#if VARCO_DEBUG
                Debug.LogError("[VRM] 얼굴 메시를 찾을 수 없습니다!");
#endif
                return null;
            }
            
            // LipSyncPlayer 추가
            var player = vrmRoot.GetComponent<LipSyncPlayer>();
            if (player == null)
            {
                player = vrmRoot.AddComponent<LipSyncPlayer>();
            }
            
            // VRM 프로파일 생성 및 적용
            var profile = CreateVRMProfile();
            player.SetTarget(faceMesh);
            player.SetProfile(profile);
            
            // AudioSource 추가 (없으면)
            if (vrmRoot.GetComponent<AudioSource>() == null)
            {
                vrmRoot.AddComponent<AudioSource>();
            }
            
#if VARCO_DEBUG
            Debug.Log($"[VRM] 립싱크 설정 완료! Face Mesh: {faceMesh.name}");
#endif
            return player;
        }
    }
}
