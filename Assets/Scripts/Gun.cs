﻿using Photon;
using UnityEngine;

public class Gun : PunBehaviour, IPunObservable
{
    private bool _isOutOfAmmo;
    private float _gatheringTimer;

    private RaycastHit[] _targetRaycasts = new RaycastHit[1];
    private Vector3 _targetRayCastPoint;
    private Mineral _targetMineral;

    [SerializeField] private Transform _gunBarrel;
    [SerializeField] private LineRenderer _lineRenderer;
    [SerializeField] private PlayerController _owner;
    [SerializeField] private Bullet __bullet;
    [SerializeField] private float _shootForce;
    [SerializeField] private float _reloadDistance;
    [SerializeField] private float _reloadDuration;

    void Start()
    {
        GameManager.Instance.Respawn += OnRespawn;
    }

    private void OnRespawn()
    {
        _isOutOfAmmo = false;
    }

    void Update()
    {
        if (_targetRayCastPoint != Vector3.zero)
        {
            _lineRenderer.positionCount = 2;
            _lineRenderer.SetPosition(0, _gunBarrel.transform.position);
            _lineRenderer.SetPosition(1, _targetRayCastPoint);
        }
        else
            _lineRenderer.positionCount = 0;

        if (!_owner.pw.isMine || !PhotonNetwork.connected)
            return;

        //Shoot
        if (_owner.playerMode == PlayerMode.Normal && Input.GetMouseButtonDown(0) && !_isOutOfAmmo)
        {
            var HeadTrans = _owner.HeadTrans;
            var go = PhotonNetwork.Instantiate("Bullet", HeadTrans.position + HeadTrans.forward, Quaternion.identity, 0);
            go.GetComponent<Rigidbody>().AddForce(HeadTrans.forward * _shootForce);
            go.GetComponent<Bullet>().controller = _owner;

            _isOutOfAmmo = true;
        }

        //Reload
        if (Input.GetMouseButtonDown(1) && _isOutOfAmmo)
        {
            if (!_targetMineral)
            {
                var hits = Physics.RaycastNonAlloc(
                    _owner.HeadTrans.transform.position,
                    _owner.HeadTrans.forward,
                    _targetRaycasts,
                    _reloadDistance, LayerMask.GetMask("Mineral"));

                if (hits == 1)
                {
                    var raycastHit = _targetRaycasts[0];
                    
                    float dist = Vector3.Distance(raycastHit.transform.position, _owner.transform.position);

                    if (dist < _reloadDistance)
                    {
                        Mineral targetMineral = raycastHit.transform.GetComponent<Mineral>();

                        if (targetMineral.IsAvailable)
                        {
                            _targetRayCastPoint = raycastHit.point;
                            _targetMineral = targetMineral;
                        }
                    }
                }
            }
        }

        if (Input.GetMouseButton(1) && _targetMineral)
        {
            //We're gathering!
            _gatheringTimer += Time.deltaTime / _reloadDuration;

            //Did we finish gathering?
            if (_gatheringTimer >= 1)
                FinishGathering(true);
            else
            {
                //Did we break by moving?
                float dist = Vector3.Distance(_targetMineral.transform.position, _owner.transform.position);

                if (dist > _reloadDistance)
                    FinishGathering(false);
            }
        }
        else if (_targetMineral)
        {
            //We interrupted the gathering process.
            FinishGathering(false);
        }
    }

    private void FinishGathering(bool success)
    {
        if (success)
            _targetMineral.Owner.RPC("RPCDisable", PhotonTargets.All, false);

        _isOutOfAmmo = !success;
        _targetMineral = null;
        _gatheringTimer = 0;
        _targetRayCastPoint = Vector3.zero;
        _lineRenderer.positionCount = 0;
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.isWriting)
        {
            stream.SendNext(_targetRayCastPoint);
        }
        else
        {
            _targetRayCastPoint = (Vector3)stream.ReceiveNext();
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.Respawn += OnRespawn;
    }
}
