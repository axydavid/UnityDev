using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

public class MultiplayerBufferZone : MonoBehaviour 
{
    public static bool opponentReady = false, playerReady = false, clientReady = false, dataSent=false, executing = false;
    public static int playersReady=0;
    public static Dictionary<CharacterState.Faction, List<ICharacterTarget>> allTargetCharacters = new Dictionary<CharacterState.Faction, List<ICharacterTarget>>();
    CharacterState.Faction chFaction;
    bool tmpexecuting = false;
    List<CharacterModule> subject=new List<CharacterModule>();

    PhotonView photonView = new PhotonView();
    Person person = new Person();
    List<Person> playerPersons=new List<Person>(),opponentPersons=new List<Person>();


    // Use this for initialization
    void Start () {

	}
    void Update()
    { 

	}

    void Awake()
    {
        PhotonNetwork.OnEventCall += this.OnEvent;
    }
    private void OnEvent(byte eventcode, object content, int senderid)
    {
        if (eventcode == 0)
        {
            PhotonPlayer sender = PhotonPlayer.Find(senderid);  // who sent this?
            Debug.LogError("0: Receiving ready signal");
            opponentReady=(bool)content;
            playersReady++;

        }
        if (eventcode == 1)
        {
            List<Person> receivedPersons = new List<Person>();
            PhotonPlayer sender = PhotonPlayer.Find(senderid);  // who sent this?
            Debug.LogError("1: Receiving Client character actions");
            receivedPersons = (List<Person>)Deserialize((byte[])content);
            setTargetCharIC(receivedPersons, CharacterState.Faction.Enemy);
            playersReady++;
        }
        if (eventcode == 2)
        {
            List<Person> receivedPersons = new List<Person>();
            PhotonPlayer sender = PhotonPlayer.Find(senderid);  // who sent this?
            Debug.LogError("2: Receiving Master character actions");
            receivedPersons = (List<Person>)Deserialize((byte[])content);
            setTargetCharIC(receivedPersons, CharacterState.Faction.Player);
            playersReady++;

        }
        if (eventcode == 3)
        {
            PhotonPlayer sender = PhotonPlayer.Find(senderid);  // who sent this?
            Debug.LogError("3: Receiving Execution");
            executing = (bool)content;
            
        }
        if (eventcode == 4)
        {
            //PhotonPlayer sender = PhotonPlayer.Find(senderid);  // who sent this?
            Debug.LogError("4: Character death synchronisation");
            killTargetChar((Character)content,CharacterState.Faction.Player);
            killTargetChar((Character)content, CharacterState.Faction.Enemy);

        }
    }
    // Update is called once per frame
    public void MasterSend()
    {
        List<Person> buffer = new List<Person>();
        buffer.AddRange(getTargetCharIC(CharacterState.Faction.Player));
        PhotonNetwork.RaiseEvent(2, Serialize(buffer), true, null);
        //the photom supports limited amount of types, need to serialize them
        //we cannot send commands, need to redo them
        //send all stuff


    }
    public void ClientSend()
    {
        List<Person> buffer = new List<Person>();
        allTargetCharacters = LevelManager.allTargetCharacters;
        buffer.AddRange(getTargetCharIC(CharacterState.Faction.Player));
        PhotonNetwork.RaiseEvent(1, Serialize(buffer), true, null);
        //send all stuff

    }


    public List<Person> getTargetCharIC(CharacterState.Faction chFac)
    {
        List<Person> buffer = new List<Person>();

        foreach (CharacterAspect characterAspect in allTargetCharacters[chFac])
        {
           CharacterCommander charCommander = characterAspect.ch.GetModule<CharacterCommander>();
           CharacterStatus charStatus = characterAspect.ch.GetModule<CharacterStatus>();

            person.name = charStatus.name;

                       //person.commands = charCommander.allCommands;
            buffer.Add(person);
        }
        return buffer;
    }


    public void setTargetCharIC(List<Person> buffer, CharacterState.Faction chFac)
    {
        int i = 0;
        foreach (CharacterAspect characterAspect in allTargetCharacters[chFac])
        {
            CharacterCommander charCommander = characterAspect.ch.GetModule<CharacterCommander>();
            CharacterStatus charStatus = characterAspect.ch.GetModule<CharacterStatus>();

            //charCommander.RemoveAllCommands();
           // charCommander.RemoveAllSubCommanders();

            if (!charStatus.name.Equals(buffer[i].name)) 
            { 
                Debug.LogError("Soldiers name not corresponsing(IC)");

            }

            //  foreach (Command commandss in buffer[i].commands)
            //  {
            //      charCommander.AddCommand(commandss);
            //  }

            i++;
        }
    }

    public List<SyncPerson> getTargetCharPQH(CharacterState.Faction chFac)
    {
        SyncPerson syncPerson = new SyncPerson();
        List<SyncPerson> buffer = new List<SyncPerson>();
        int i=0;
        foreach (CharacterAspect characterAspect in LevelManager.allTargetCharacters[chFac])
        {
            CharacterCommander charCommander = characterAspect.ch.GetModule<CharacterCommander>();
            CharacterStatus charStatus = characterAspect.ch.GetModule<CharacterStatus>();

            syncPerson.ID = i;
            syncPerson.name = charStatus.name;
            syncPerson.HP = charStatus.health;
            syncPerson.pos = charStatus.gameObject.transform.position;
            syncPerson.rot = charStatus.gameObject.transform.rotation;

            buffer.Add(syncPerson);
            i++;
        }
        return buffer;
    }

    public void setTargetCharPQH(List<SyncPerson> buffer, CharacterState.Faction chFac)
    {
        int i = 0;
        foreach (CharacterAspect characterAspect in LevelManager.allTargetCharacters[chFac])
        {
            CharacterStatus charStatus = characterAspect.ch.GetModule<CharacterStatus>();

            if (!charStatus.name.Equals(buffer[i].name))
            {
                Debug.LogError("Soldiers name not corresponsing(PQH)");

            }

            charStatus.name=buffer[i].name;
            charStatus.health=buffer[i].HP;
            charStatus.gameObject.transform.position=buffer[i].pos;
            charStatus.gameObject.transform.rotation=buffer[i].rot;

            i++;
        }
    }

    public void killTargetChar(Character target, CharacterState.Faction chFac)
    {
        foreach (CharacterAspect characterAspect in LevelManager.allTargetCharacters[chFac])
        {
            CharacterStatus charStatus = characterAspect.ch.GetModule<CharacterStatus>();

            if (!charStatus.name.Equals(target.name))continue;

            target.GetModule<CharacterStatus>().health = 0;
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.isWriting)
        {
            // We own this player: send the others our data
            stream.SendNext(MultiplayerBufferZone.playerReady);
            stream.SendNext(getTargetCharPQH(CharacterState.Faction.Player));
            stream.SendNext(getTargetCharPQH(CharacterState.Faction.Enemy));

        }
        else
        {
            // Network player, receive data
            MultiplayerBufferZone.opponentReady = (bool)stream.ReceiveNext();
            setTargetCharPQH((List<SyncPerson>)stream.ReceiveNext(),CharacterState.Faction.Player);
            setTargetCharPQH((List<SyncPerson>)stream.ReceiveNext(), CharacterState.Faction.Enemy);


        }

    }
    private byte[] Serialize(List<Person> obj)
    {
        if (obj == null)
            return null;

        BinaryFormatter bf = new BinaryFormatter();
        System.IO.MemoryStream ms = new MemoryStream();
        bf.Serialize(ms, obj);

        return ms.ToArray();
    }

    // Convert a byte array to an Object
    private List<Person> Deserialize(byte[] arrBytes)
    {
        MemoryStream memStream = new MemoryStream();
        BinaryFormatter binForm = new BinaryFormatter();
        memStream.Write(arrBytes, 0, arrBytes.Length);
        memStream.Seek(0, SeekOrigin.Begin);
        List<Person> obj = (List<Person>)binForm.Deserialize(memStream);

        return obj;
    }


}

[System.Serializable]
public class Person
{
    public Person()
    {
        this.name = string.Empty;
        this.ID = new int();
        this.HP = new float();
        //this.commands = new List<Command>();
    }

    public string name { get; set; }
    public int ID { get; set; }
    public float HP { get; set; }
    //public List<Command> commands { get; set; }
}

[System.Serializable]
public class SyncPerson
{
    public SyncPerson()
    {
        this.ID = new int();
        this.name = string.Empty;
        this.HP = new float();
        this.pos = new Vector3();
        this.rot = new Quaternion();
    }

    public string name { get; set; }
    public int ID { get; set; }
    public float HP { get; set; }
    public Vector3 pos { get; set; }
    public Quaternion rot { get; set; }
}

