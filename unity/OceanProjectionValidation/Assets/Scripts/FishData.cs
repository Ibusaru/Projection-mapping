using System;

[Serializable]
public class FishData
{
    public string id;
    public string nickname;
    public string species;
    public string main_color;
    public string sub_color;
    public string pattern;
    public string size;
    public string personality;
    public string texture_path;
    public string texture_url;
    public string created_at;
    public string updated_at;
}

[Serializable]
public class FishDataList
{
    public FishData[] items;
}
