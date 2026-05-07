using UnityEngine;
using UnityEngine.UI; // ��������UI�����ռ䣬����ʹ�� Button ��

public class UIManager : MonoBehaviour
{
    [Header("UI �������")]
    public GameObject myPanel;      // ����������Ҫ����/��ʾ�����

    [Header("��ť����")]
    public Button openButton;       // ����һ�еġ��򿪡���ť
    public Button closeButton;
    public Button Buttonclose;      // ����������еġ��رա���ť

    void Start()
    {
        // 1. ��Ϸ��ʼʱ����ʼ������壨�̳��Է�������
        if (myPanel != null)
        {
            myPanel.SetActive(false);
        }

        // 2. Ϊ���򿪡���ť�󶨵���¼�
        if (openButton != null)
        {
            // �� openButton �����ʱ��ִ�� OpenPanel ����
            openButton.onClick.AddListener(OpenPanel);
        }

        // 3. Ϊ���رա���ť�󶨵���¼�
        if (closeButton != null)
        {
            // �� closeButton �����ʱ��ִ�� ClosePanel ����
            closeButton.onClick.AddListener(ClosePanel);
        }
    }

    // --- �����Ǿ����ִ�з��� ---

    // �����ķ���
    void OpenPanel()
    {
        myPanel.SetActive(true);
    }

    // �ر����ķ���
    void ClosePanel()
    {
        myPanel.SetActive(false);
    }
}