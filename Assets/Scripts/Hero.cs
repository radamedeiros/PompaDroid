using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Hero : Actor
{
    public float walkSpeed = 2;
    public float runSpeed = 5;

    // Variáveis de corrida
    bool isRunning;
    bool isMoving;
    float lastWalk = 0.0f;
    public bool canRun = true;
    float tapAgainToRunTime = 0.2f;
    Vector3 lastWalkVector = Vector3.zero;

    Vector3 curDirection;
    bool isFacingLeft;

    // Variáveis de pulo
    bool isJumpLandAnim;
    bool isJumpingAnim;

    // Controle de inputs
    private PlayerControls controls;
    private Vector2 moveInput;
    private bool jumpInput;
    private bool attackInput;

    public float jumpForce = 1750;
    private float jumpDuration = 0.2f;
    private float lastJumpTime;

    // Variáveis de ataque
    bool isAttackingAnim;
    float lastAttackTime;
    float attackLimit = 0.14f;

    // Variáveis de entrada automática
    public Walker walker;
    public bool isAutoPiloting;
    public bool controllable = true;

    // Variáveis para ataque no pulo
    public bool canJumpAttack = true;
    private int currentAttackChain = 1;
    public int evaluatedAttackChain = 0;
    public AttackData jumpAttack;

    bool isHurtAnim;

    // Variáveis para ataque correndo
    public AttackData runAttack;
    public float runAttackForce = 1.8f; // Distância do ataque

    // Variáveis para combo
    public AttackData normalAttack2;
    public AttackData normalAttack3;

    float chainComboTimer;
    public float chainComboLimit = 0.3f;
    const int maxCombo = 3;

    // Variáveis de tolerância a dano
    public float hurtTolerance;
    public float hurtLimit = 20;
    public float recoveryRate = 5;

    bool isPickingUpAnim;
    bool weaponDropPressed = false; // Quando o jogador pula
    public bool hasWeapon;

    public bool canJump = true;

    public SpriteRenderer powerupSprite;
    public Powerup nearbyPowerup;
    public Powerup currentPowerup;
    public GameObject powerupRoot;

    public AudioClip hit2Clip; // SFX do ataque forte do herói

    public GameManager gameManager;
    public JumpCollider jumpCollider;

    private void Awake()
    {
        // Inicializa os controles de input
        controls = new PlayerControls();

        // Associa os métodos aos eventos de input
        controls.Gameplay.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Gameplay.Move.canceled += ctx => moveInput = Vector2.zero;

        controls.Gameplay.Jump.performed += ctx => jumpInput = true;
        controls.Gameplay.Jump.canceled += ctx => jumpInput = false;

        controls.Gameplay.Attack.performed += ctx => attackInput = true;
        controls.Gameplay.Attack.canceled += ctx => attackInput = false;
    }

    private void OnEnable()
    {
        // Habilita o controle quando o objeto é ativado
        controls.Gameplay.Enable();
    }

    private void OnDisable()
    {
        // Desabilita o controle quando o objeto é desativado
        controls.Gameplay.Disable();
    }

    protected override void Start()
    {
        base.Start();
        lifeBar = GameObject.FindGameObjectWithTag("HeroLifeBar").GetComponent<LifeBar>();
        lifeBar.SetProgress(currentLife / maxLife);
    }

    public override void Update()
    {
        base.Update();

        if (!isAlive)
            return;

        isAttackingAnim =
            baseAnim.GetCurrentAnimatorStateInfo(0).IsName("attack1") ||
            baseAnim.GetCurrentAnimatorStateInfo(0).IsName("attack2") ||
            baseAnim.GetCurrentAnimatorStateInfo(0).IsName("attack3") ||
            baseAnim.GetCurrentAnimatorStateInfo(0).IsName("jump_attack") ||
            baseAnim.GetCurrentAnimatorStateInfo(0).IsName("run_attack");

        isJumpLandAnim =
            baseAnim.GetCurrentAnimatorStateInfo(0).IsName("jump_land");
        isJumpingAnim =
            baseAnim.GetCurrentAnimatorStateInfo(0).IsName("jump_rise") ||
            baseAnim.GetCurrentAnimatorStateInfo(0).IsName("jump_fall");

        isHurtAnim =
            baseAnim.GetCurrentAnimatorStateInfo(0).IsName("hurt");

        isPickingUpAnim =
            baseAnim.GetCurrentAnimatorStateInfo(0).IsName("pickup");

        if (isAutoPiloting)
            return;

        curDirection = new Vector3(moveInput.x, 0, moveInput.y);
        curDirection.Normalize();

        if (!isAttackingAnim)
        {
            if (chainComboTimer > 0)
            {
                chainComboTimer -= Time.deltaTime;
                if (chainComboTimer < 0)
                {
                    chainComboTimer = 0;
                    currentAttackChain = 0;
                    evaluatedAttackChain = 0;
                    baseAnim.SetInteger("CurrentChain", currentAttackChain);
                    baseAnim.SetInteger("EvaluatedChain", evaluatedAttackChain);
                }
            }
            if (moveInput == Vector2.zero)
            {
                Stop();
                isMoving = false;
            }
            else if (!isMoving && moveInput != Vector2.zero)
            {
                isMoving = true;
                float dotProduct = Vector3.Dot(curDirection, lastWalkVector);

                if (canRun && Time.time < lastWalk + tapAgainToRunTime && dotProduct > 0)
                    Run();
                else
                {
                    Walk();
                    lastWalkVector = curDirection;
                    lastWalk = Time.time;
                }
            }
        }

        if (jumpInput && hasWeapon)
        {
            weaponDropPressed = true;
            DropWeapon();
        }

        if (weaponDropPressed && !jumpInput)
            weaponDropPressed = false;

        if (jumpInput && canJump && !isKnockedOut && jumpCollider.CanJump(curDirection, frontVector) &&
            !isJumpLandAnim && !isAttackingAnim && !isPickingUpAnim && !weaponDropPressed &&
            (isGrounded || (isJumpingAnim && Time.time < lastJumpTime + jumpDuration)))
        {
            Jump(curDirection);
        }

        if (attackInput && Time.time >= lastAttackTime + attackLimit && isGrounded && !isPickingUpAnim)
        {
            if (nearbyPowerup != null && nearbyPowerup.CanEquip())
            {
                lastAttackTime = Time.time;
                Stop();
                PickupWeapon(nearbyPowerup);
            }
        }

        if (attackInput && Time.time >= lastAttackTime + attackLimit && !isKnockedOut && !isPickingUpAnim)
        {
            lastAttackTime = Time.time;
            Attack();
        }

        if (hurtTolerance < hurtLimit)
        {
            hurtTolerance += Time.deltaTime * recoveryRate;
            hurtTolerance = Mathf.Clamp(hurtTolerance, 0, hurtLimit);
        }
    }

    private void FixedUpdate()
    {
        if (!isAlive)
            return;

        if (!isAutoPiloting)
        {
            Vector3 moveVector = curDirection * speed;

            if (isGrounded && !isAttackingAnim && !isJumpLandAnim && !isKnockedOut && !isHurtAnim)
            {
                body.MovePosition(transform.position + moveVector * Time.fixedDeltaTime);
                baseAnim.SetFloat("Speed", moveVector.magnitude);

                if (moveVector != Vector3.zero && isGrounded && !isKnockedOut && !isAttackingAnim)
                {
                    if (moveVector.x != 0)
                        isFacingLeft = moveVector.x < 0;
                    FlipSprite(isFacingLeft);
                }
            }
        }
    }

    protected override void OnCollisionEnter(Collision collision)
    {
        base.OnCollisionEnter(collision);
        if (collision.collider.name == "Floor")
            canJumpAttack = true;
    }

    protected override IEnumerator KnockdownRoutine()
    {
        body.useGravity = true;
        return base.KnockdownRoutine();
    }

    public override void TakeDamage(float value, Vector3 hitVector, bool knockdown = false)
    {
        hurtTolerance -= value;
        if (hurtTolerance <= 0 || !isGrounded)
        {
            hurtTolerance = hurtLimit;
            knockdown = true;
        }
        if (hasWeapon)
            DropWeapon();
        base.TakeDamage(value, hitVector, knockdown);
    }

    public override bool CanWalk()
    {
        return (isGrounded && !isAttackingAnim && !isJumpLandAnim && !isKnockedOut && !isHurtAnim);
    }

    public void Stop()
    {
        speed = 0;
        isRunning = false;
        baseAnim.SetBool("IsRunning", isRunning);
        baseAnim.SetFloat("Speed", speed);
    }

    public void Walk()
    {
        speed = walkSpeed;
        isRunning = false;
        baseAnim.SetBool("IsRunning", isRunning);
        baseAnim.SetFloat("Speed", speed);
    }

    public void Run()
    {
        speed = runSpeed;
        isRunning = true;
        baseAnim.SetBool("IsRunning", isRunning);
        baseAnim.SetFloat("Speed", speed);
    }

    void Jump(Vector3 direction)
    {
        if (!isJumpingAnim)
        {
            baseAnim.SetTrigger("Jump");
            lastJumpTime = Time.time;
            Vector3 horizontalVector = new Vector3(direction.x, 0, direction.z) * speed * 40;
            body.AddForce(horizontalVector, ForceMode.Force);
        }

        Vector3 verticalVector = Vector3.up * jumpForce * Time.deltaTime;
        body.AddForce(verticalVector, ForceMode.Force);
    }

    public override void Attack()
    {
        if (currentAttackChain <= maxCombo)
        {
            if (!isGrounded)
            {
                if (isJumpingAnim && canJumpAttack)
                {
                    canJumpAttack = false;
                    currentAttackChain = 1;
                    evaluatedAttackChain = 0;
                    baseAnim.SetInteger("EvaluatedChain", evaluatedAttackChain);
                    baseAnim.SetInteger("CurrentChain", currentAttackChain);

                    body.velocity = Vector3.zero;
                    body.useGravity = false;
                }
            }
            else
            {
                if (isRunning)
                {
                    body.AddForce((Vector3.up + (frontVector * 5)) * runAttackForce, ForceMode.Impulse);

                    currentAttackChain = 1;
                    evaluatedAttackChain = 0;
                    baseAnim.SetInteger("CurrentChain", currentAttackChain);
                    baseAnim.SetInteger("EvaluatedChain", evaluatedAttackChain);
                }
                else
                {
                    if (currentAttackChain == 0 || chainComboTimer == 0)
                    {
                        currentAttackChain = 1;
                        evaluatedAttackChain = 0;
                    }
                    baseAnim.SetInteger("EvaluatedChain", evaluatedAttackChain);
                    baseAnim.SetInteger("CurrentChain", currentAttackChain);
                }
            }
        }
    }

    private void AnalyzeNormalAttack(AttackData attackData, int attackChain, Actor actor, Vector3 hitPoint, Vector3 hitVector)
    {
        actor.EvaluateAttackData(attackData, hitVector, hitPoint);
        currentAttackChain = attackChain;
        chainComboTimer = chainComboLimit;
    }

    private void AnalyzeSpecialAttack(AttackData attackData, Actor actor, Vector3 hitPoint, Vector3 hitVector)
    {
        actor.EvaluateAttackData(attackData, hitVector, hitPoint);
        chainComboTimer = chainComboLimit;
    }

    protected override void HitActor(Actor actor, Vector3 hitPoint, Vector3 hitVector)
    {
        if (baseAnim.GetCurrentAnimatorStateInfo(0).IsName("attack1"))
        {
            AttackData attackData = hasWeapon ? currentPowerup.attackData1 : normalAttack;
            AnalyzeNormalAttack(attackData, 2, actor, hitPoint, hitVector);
            PlaySFX(hitClip);
            if (hasWeapon)
                currentPowerup.Use();
        }
        else if (baseAnim.GetCurrentAnimatorStateInfo(0).IsName("attack2"))
        {
            AttackData attackData = hasWeapon ? currentPowerup.attackData2 : normalAttack2;
            AnalyzeNormalAttack(attackData, 3, actor, hitPoint, hitVector);
            PlaySFX(hitClip);
            if (hasWeapon)
                currentPowerup.Use();
        }
        else if (baseAnim.GetCurrentAnimatorStateInfo(0).IsName("attack3"))
        {
            AttackData attackData = hasWeapon ? currentPowerup.attackData3 : normalAttack3;
            AnalyzeNormalAttack(attackData, 1, actor, hitPoint, hitVector);
            PlaySFX(hit2Clip);
            if (hasWeapon)
                currentPowerup.Use();
        }
        else if (baseAnim.GetCurrentAnimatorStateInfo(0).IsName("jump_attack"))
        {
            AnalyzeSpecialAttack(jumpAttack, actor, hitPoint, hitVector);
            PlaySFX(hit2Clip);
        }
        else if (baseAnim.GetCurrentAnimatorStateInfo(0).IsName("run_attack"))
        {
            AnalyzeSpecialAttack(runAttack, actor, hitPoint, hitVector);
            PlaySFX(hit2Clip);
        }
    }

    public void DidChain(int chain)
    {
        evaluatedAttackChain = chain;
        baseAnim.SetInteger("EvaluatedChain", evaluatedAttackChain);
    }

    protected override void DidLand()
    {
        base.DidLand();
        Walk();
    }

    public void DidJumpAttack()
    {
        body.useGravity = true;
    }

    public void AnimateTo(Vector3 position, bool shouldRun, Action callback)
    {
        if (shouldRun)
            Run();
        else
            Walk();
        walker.MoveTo(position, callback);
    }

    public void UseAutopilot(bool useAutopilot)
    {
        isAutoPiloting = useAutopilot;
        walker.enabled = useAutopilot;
    }

    public override void DidHitObject(Collider collider, Vector3 hitPoint, Vector3 hitVector)
    {
        Container containerObject = collider.GetComponent<Container>();

        if (isAttackingAnim && containerObject != null)
        {
            containerObject.Hit(hitPoint);
            PlaySFX(hitClip);
            if (containerObject.CanBeOpened() && collider.tag != gameObject.tag)
                containerObject.Open(hitPoint);
        }
        else
            base.DidHitObject(collider, hitPoint, hitVector);
    }

    public void PickupWeapon(Powerup powerup)
    {
        baseAnim.SetTrigger("PickupPowerup");
    }

    public void DidPickupWeapon()
    {
        if (nearbyPowerup != null && nearbyPowerup.CanEquip())
        {
            Powerup powerup = nearbyPowerup;
            hasWeapon = true;
            currentPowerup = powerup;
            nearbyPowerup = null;
            powerupRoot = currentPowerup.rootObject;
            powerup.user = this;

            currentPowerup.body.velocity = Vector3.zero;
            powerupRoot.SetActive(false);
            Walk();

            powerupSprite.enabled = true;
            canRun = false;
            canJump = false;
        }
    }

    public void DropWeapon()
    {
        powerupRoot.SetActive(true);
        powerupRoot.transform.position = transform.position + Vector3.up;
        currentPowerup.body.AddForce(Vector3.up * 100);

        powerupRoot = null;
        currentPowerup.user = null;
        currentPowerup = null;
        nearbyPowerup = null;

        powerupSprite.enabled = false;
        canRun = true;
        hasWeapon = false;
        canJump = true;
    }

    private void OnTriggerEnter(Collider collider)
    {
        if (collider.gameObject.layer == LayerMask.NameToLayer("Powerup"))
        {
            Powerup powerup = collider.gameObject.GetComponent<Powerup>();
            if (powerup != null)
                nearbyPowerup = powerup;
        }
    }

    private void OnTriggerExit(Collider collider)
    {
        if (collider.gameObject.layer == LayerMask.NameToLayer("Powerup"))
        {
            Powerup powerup = collider.gameObject.GetComponent<Powerup>();
            if (powerup == nearbyPowerup)
                nearbyPowerup = null;
        }
    }

    protected override void Die()
    {
        base.Die();
        gameManager.GameOver();
    }
}
