FROM mcr.microsoft.com/azure-functions/python:4-python3.11

# Set environment variables for Azure Functions
ENV AzureWebJobsScriptRoot=/home/site/wwwroot
ENV AzureFunctionsJobHost__Logging__Console__IsEnabled=true
ENV AZURE_FUNCTIONS_ENVIRONMENT=Production
ENV CONTAINER_VERSION="v8.1_fixed_verify"

# Install git and build essentials for requirements
# RUN apt-get update && apt-get install -y git build-essential && rm -rf /var/lib/apt/lists/*
RUN apt-get update && apt-get install -y git build-essential && rm -rf /var/lib/apt/lists/* && \
    # Create a virtual environment to isolate dependencies
    python -m venv /home/site/venv

ENV PATH="/home/site/venv/bin:${PATH}"

# Copy requirements folder and main requirements
COPY requirements/ /home/site/wwwroot/requirements/
COPY requirements.txt /home/site/wwwroot/requirements.txt

# Install remaining packages
RUN pip install --no-cache-dir -r /home/site/wwwroot/requirements.txt

# Set environment variable for NLTK data path
ENV NLTK_DATA=/home/site/nltk_data

# Set environment variables for caching
ENV TRANSFORMERS_CACHE=/home/site/model_cache
ENV HF_HOME=/home/site/model_cache
ENV EVALUATE_CACHE_DIR=/home/site/evaluate_cache

RUN mkdir -p $NLTK_DATA && \
    python -c "import nltk; nltk.data.path.append('$NLTK_DATA'); nltk.download('punkt', download_dir='$NLTK_DATA'); nltk.download('wordnet', download_dir='$NLTK_DATA'); nltk.download('omw-1.4', download_dir='$NLTK_DATA')" && \
    # Verify installation
    python -c "import nltk; import rouge_score; from rouge_score import rouge_scorer; print('NLTK version:', nltk.__version__); print('NLTK data path:', nltk.data.path); print('Rouge Score imported successfully')" && \
    # Create directories for caching
    mkdir -p /home/site/model_cache /home/site/evaluate_cache && \
    # Pre-download common models
    python -c "from transformers import AutoModel, AutoTokenizer; \
               AutoModel.from_pretrained('bert-base-uncased'); \
               AutoTokenizer.from_pretrained('bert-base-uncased');" && \
    # Pre-download bert-score models (only bert-base-uncased)
    python -c "from bert_score import BERTScorer; \
               scorer = BERTScorer(model_type='bert-base-uncased', lang='en');" && \
    # Pre-download evaluate metrics
    python -c "import evaluate; \
               evaluate.load('rouge'); \
               evaluate.load('bertscore'); \
               evaluate.load('meteor'); \
               evaluate.load('sacrebleu');"

# Copy function code
COPY . /home/site/wwwroot
