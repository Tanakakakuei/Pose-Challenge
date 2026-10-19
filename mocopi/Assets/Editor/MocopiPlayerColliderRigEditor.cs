using UnityEditor;

[CustomEditor(typeof(MocopiPlayerColliderRig)), CanEditMultipleObjects]
public class MocopiPlayerColliderRigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("半径は実際のメートル単位です（0.05 = 半径5cm）。モデルや親のScaleでは拡大しません。\n" +
            "Offsetもメートル単位で、向きだけ各ボーンに合わせます。\n" +
            "調整はこのコンポーネントで行ってください。生成された子のColliderは自動更新されます。\n" +
            "マウス操作の判定はGame Loop ControllerのDebug Mouse Collider Radiusで別に設定します。", MessageType.Info);
        DrawDefaultInspector();
    }
}
