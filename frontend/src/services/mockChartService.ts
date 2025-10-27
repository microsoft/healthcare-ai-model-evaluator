export interface ChartSection {
    title: string;
    content: string;
}

export interface PatientInfo {
    name: string;
    age: number;
    gender: string;
    ethnicity: string;
    dateOfVisit: string;
    mrn: string;
}

export interface ChartData {
    patientInfo?: PatientInfo;
    sections: ChartSection[];
}

export const mockChartData: ChartData = {
    patientInfo: {
        name: 'Jane Doe',
        age: 45,
        gender: 'Female',
        ethnicity: 'Caucasian',
        dateOfVisit: '2024-03-15',
        mrn: '123456789'
    },
    sections: [
        {
            title: 'Chief Complaint',
            content: 'Severe headache and dizziness for the past 24 hours'
        },
        {
            title: 'History of Present Illness',
            content: 'Patient presents with severe, throbbing headache that began 24 hours ago. Pain is bilateral, rated 8/10, and accompanied by photophobia, phonophobia, and mild nausea. No vomiting. Patient reports similar episodes in the past, typically occurring 2-3 times per month. Current episode is more severe than usual. No recent head trauma or fever.'
        },
        {
            title: 'Past Medical History',
            content: 'Migraine (diagnosed 2015)\nHypertension (diagnosed 2018)\nGastroesophageal reflux disease'
        },
        {
            title: 'Past Surgical History',
            content: 'Appendectomy (2010)\nLaparoscopic cholecystectomy (2016)'
        },
        {
            title: 'Current Medications',
            content: 'Sumatriptan 100mg PRN for migraines\nPropranolol 40mg daily\nOmeprazole 20mg daily\nIbuprofen 400mg PRN for pain'
        },
        {
            title: 'Allergies',
            content: 'Penicillin (hives)\nSulfa drugs (rash)'
        },
        {
            title: 'Social History',
            content: 'Works as a high school teacher. Non-smoker. Occasional alcohol use (1-2 drinks/week). Lives with husband and two children. Regular exercise 3 times/week.'
        },
        {
            title: 'Family History',
            content: 'Mother: Migraines, Hypertension\nFather: Type 2 Diabetes\nSister: Migraines'
        },
        {
            title: 'Review of Systems',
            content: `GENERAL: Denies fever, chills, or recent weight changes
HEENT: Photophobia and phonophobia present. No vision changes or tinnitus
CARDIOVASCULAR: No chest pain or palpitations
RESPIRATORY: No shortness of breath or cough
GASTROINTESTINAL: Mild nausea, no vomiting or abdominal pain
MUSCULOSKELETAL: No neck stiffness or muscle weakness
NEUROLOGICAL: Alert and oriented, no focal deficits
PSYCHIATRIC: No anxiety or depression`
        },
        {
            title: 'Physical Examination',
            content: `VITAL SIGNS:
BP: 140/90 mmHg | HR: 82 bpm | Temp: 98.6°F | RR: 16 | SpO2: 98% on RA

GENERAL: Alert, oriented, in mild distress due to pain
HEENT: PERRL, EOMI, no papilledema
NECK: Supple, no meningeal signs
CARDIOVASCULAR: Regular rate and rhythm, no murmurs
RESPIRATORY: Clear to auscultation bilaterally
NEUROLOGICAL: CN II-XII intact, normal strength and sensation in all extremities`
        },
        {
            title: 'Laboratory Results',
            content: `CBC:
- WBC: 7.2 K/µL
- Hgb: 13.8 g/dL
- Plt: 250 K/µL

Basic Metabolic Panel:
- Na: 140 mEq/L
- K: 4.0 mEq/L
- Cl: 101 mEq/L
- CO2: 24 mEq/L
- BUN: 15 mg/dL
- Cr: 0.9 mg/dL
- Glucose: 95 mg/dL`
        },
        {
            title: 'Imaging',
            content: 'CT Head without contrast: No acute intracranial abnormality. No mass effect or midline shift. No hemorrhage or infarct.'
        },
        {
            title: 'Assessment',
            content: '1. Acute migraine with aura, severe (ICD-10: G43.109)\n2. Hypertension, essential (ICD-10: I10)'
        },
        {
            title: 'Plan',
            content: `1. Acute migraine treatment:
   - Administer Toradol 30mg IV
   - Compazine 10mg IV for nausea
   - IV fluids: NS 1L bolus

2. Discharge medications:
   - Continue current medications
   - Add Nurtec ODT 75mg PRN for acute migraines
   
3. Follow-up:
   - Schedule follow-up with PCP in 1 week
   - Neurology referral for migraine management
   
4. Patient education:
   - Migraine trigger avoidance
   - Stress management techniques
   - When to seek emergency care`
        }
    ]
};

export const chartService = {
    getChartData: async (): Promise<ChartData> => {
        return mockChartData;
    }
}; 